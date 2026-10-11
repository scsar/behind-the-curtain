using System.Globalization;

namespace BehindTheCurtain.Core.Sim;

public static class PuppetSweep
{
    public static bool TryExpand(string[] arguments, int maximum, out List<Dictionary<string, string>> combinations, out string reason)
    {
        combinations = new() { new(StringComparer.OrdinalIgnoreCase) }; reason = "";
        var dimensions = new List<(string Name, string[] Values)>(); long count = 1;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string argument in arguments)
        {
            int equals = argument.IndexOf('=');
            if (equals <= 0) { reason = "Expected Class.name=value,value: " + argument; return false; }
            string name = argument[..equals];
            if (!names.Add(name)) { reason = "Duplicate tuning: " + name; return false; }
            var values = argument[(equals + 1)..].Split(',');
            count *= values.Length;
            if (count > maximum) { reason = "SweepMaxCombinations exceeded: " + maximum; return false; }
            dimensions.Add((name, values));
        }
        foreach (var d in dimensions)
            combinations = combinations.SelectMany(c => d.Values.Select(v => { var next = new Dictionary<string, string>(c, StringComparer.OrdinalIgnoreCase); next[d.Name] = v; return next; })).ToList();
        return true;
    }
    private sealed record Row(int Failures, int Stutter, double? Swap, double? Stop, double? Pass, double Duration, string Tuning, string? Rejection)
    {
        public double? Mean => Swap.HasValue && Stop.HasValue && Pass.HasValue ? (Swap.Value + Stop.Value + Pass.Value) / 3 : null;
        private static string Number(double? value) => value?.ToString("0.00", CultureInfo.InvariantCulture) ?? "n/a";
        public string Print(string rank) => Rejection != null ? $"{rank} | rejected | - | - | - | - | - | - | {Tuning} ({Rejection})"
            : $"{rank} | {Failures} | {Stutter} | {Number(Swap)} | {Number(Stop)} | {Number(Pass)} | {Number(Mean)} | {Number(Duration)} | {Tuning}";
    }
    private static Row Evaluate(Dictionary<string, string> values, PuppetScenarioSettings scenario, SweepSettings sweep)
    {
        if (!PuppetTuningBuilder.TryBuild(values, scenario, out var settings, out var tuning, out var reason))
            return new(0, 0, null, null, null, 0, string.Join(",", values.Select(e => e.Key + "=" + e.Value)), reason);
        int failures = 0, stutter = 0, duration = 0, runs = 0;
        var swaps = new List<double?>(); var stops = new List<double?>(); var passes = new List<double?>();
        for (ulong seed = 1; seed <= 3; seed++)
        {
            foreach (string name in new[] { "approach", "healer", "standstill", "duo", "duo-swap" })
            {
                bool duo = name is "duo" or "duo-swap";
                var r = PuppetSimulation.Run(name, seed, null, settings, scenario, "none", duo, loadout: duo ? "mixed" : "presets");
                failures += SweepMetrics.Forbidden(r, scenario); stutter += SweepMetrics.Stutter(r, sweep);
                var d = SweepMetrics.ActionDurations(r); duration += d.Total; runs += d.Runs;
                if (name == "duo-swap") swaps.Add(SweepMetrics.TargetLatency(r, scenario.DuoSwapSecond * settings!.TicksPerSecond));
                if (name == "standstill")
                {
                    int stop = r.Frames.First(f => f.Player.Z <= scenario.StandstillStop).Tick;
                    stops.Add(SweepMetrics.StandstillLatency(r, stop, settings!.TicksPerSecond));
                }
            }
            var pass = PuppetSimulation.Run("passby", seed, settings: settings, scenario: scenario);
            passes.Add(SweepMetrics.PassbyLatency(pass, scenario.FirstGuardStart, scenario.SecondGuardStart, sweep.PassbyReturnDistance));
        }
        // No eligible target change is not a zero-latency response. Missing observations remain n/a.
        double? EligibleAverage(List<double?> samples) => samples.Any(x => x.HasValue) ? samples.Where(x => x.HasValue).Average(x => x!.Value) : null;
        double? CompleteAverage(List<double?> samples) => samples.All(x => x.HasValue) ? samples.Average(x => x!.Value) : null;
        return new(failures, stutter, EligibleAverage(swaps), CompleteAverage(stops), CompleteAverage(passes), (double)duration / runs, tuning, null);
    }
    public static int Run(string[] arguments, TextWriter output, TextWriter error, SweepSettings? sweep = null, PuppetScenarioSettings? scenario = null)
    {
        sweep ??= new(); scenario ??= new();
        if (!TryExpand(arguments, sweep.SweepMaxCombinations, out var combinations, out var reason)) { error.WriteLine(reason); return 2; }
        output.WriteLine("rank | forbidden | stutter | latency(i) | latency(ii) | latency(iii) | latency mean(tick) | action duration(tick) | U7 F6");
        output.WriteLine(Evaluate(new(), scenario, sweep).Print("baseline"));
        var rows = combinations.Select(c => Evaluate(c, scenario, sweep)).OrderBy(r => r.Rejection != null)
            .ThenBy(r => r.Failures != 0).ThenBy(r => r.Stutter).ThenBy(r => r.Mean ?? double.PositiveInfinity).ToArray();
        for (int i = 0; i < rows.Length; i++) output.WriteLine(rows[i].Print((i + 1).ToString(CultureInfo.InvariantCulture)));
        return 0;
    }
}
