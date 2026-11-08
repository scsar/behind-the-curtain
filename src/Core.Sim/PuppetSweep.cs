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
        internal Dictionary<string, int> Patterns { get; init; } = new();
        internal SweepMetrics.HarassCycles Cycles { get; init; } = new();
        internal List<string> Contracts27 { get; init; } = new();
        internal List<string> Oscillations { get; init; } = new();
        internal double Dwell { get; init; }
        internal double OrbitGap { get; init; }
        internal bool NoDepartures { get; init; }
        public double? Mean => Swap.HasValue && Stop.HasValue && Pass.HasValue ? (Swap.Value + Stop.Value + Pass.Value) / 3 : null;
        private static string Number(double? value) => value?.ToString("0.00", CultureInfo.InvariantCulture) ?? "n/a";
        public string Print(string rank) => Rejection != null ? $"{rank} | rejected | - | - | - | - | - | - | - | - | - | - | {Tuning} ({Rejection})"
            : $"{rank} | {Failures} | {Stutter} | {Number(Swap)} | {Number(Stop)} | {(NoDepartures ? "0(벗어남 없음)" : Number(Pass))} | {Number(Mean)} | {Number(Duration)} | {(Cycles.Completion.HasValue ? Number(Cycles.Completion) + "%" : "n/a")} | {Number(Dwell)} | {Number(OrbitGap)} | {Contracts27.Count} | {Tuning}";
    }
    private static Row Evaluate(Dictionary<string, string> values, PuppetScenarioSettings scenario, SweepSettings sweep, string temperament)
    {
        if (!PuppetTuningBuilder.TryBuild(values, scenario, out var settings, out var tuning, out var reason))
            return new(0, 0, null, null, null, 0, string.Join(",", values.Select(e => e.Key + "=" + e.Value)), reason);
        int failures = 0, stutter = 0, duration = 0, runs = 0, dwell = 0;
        var patterns = new Dictionary<string, int>(); var cycles = new SweepMetrics.HarassCycles();
        var contracts = new List<string>(); var oscillations = new List<string>();
        void RecordOscillation(PuppetTrajectory r, string label)
        {
            if (r.MaxSwitchesInWindow <= scenario.SwitchLimit) return;
            oscillations.Add(label + " 개체 " + r.OscillationAgent + ": " + string.Join(" ",
                r.OscillationWindow.Select(c => FormattableString.Invariant($"{c.Tick} {c.Before ?? "none"}>{c.After ?? "none"}({c.Distance:F2}m)"))));
        }
        foreach (string stress in new[] { "none", "tension", "mixed" })
            foreach (string name in PuppetSimulation.Names)
            {
                var r = PuppetSimulation.Run(name, temperament, 1, null, settings, scenario, stress);
                var violations = ContractViolations(r, scenario);
                if (violations.Count > 0) contracts.Add(stress + "/" + name + " " + string.Join(", ", violations));
                RecordOscillation(r, stress + "/" + name);
            }
        var departures = new List<int>(); var gaps = new List<double>();
        var swaps = new List<double?>(); var stops = new List<double?>();
        for (ulong seed = 1; seed <= 3; seed++)
        {
            foreach (string name in new[] { "approach", "healer", "standstill", "duo", "duo-swap" })
            {
                bool duo = name is "duo" or "duo-swap";
                var r = PuppetSimulation.Run(name, temperament, seed, null, settings, scenario, "none", duo, loadout: duo ? "mixed" : "presets");
                RecordOscillation(r, "15회 none/" + name + " seed " + seed);
                foreach (var pattern in SweepMetrics.ForbiddenPatterns(r, scenario))
                    patterns[pattern.Key] = patterns.GetValueOrDefault(pattern.Key) + pattern.Value;
                cycles.Add(SweepMetrics.HarassCycle(r)); dwell += SweepMetrics.AttackDwell(r, sweep);
                failures += SweepMetrics.Forbidden(r, scenario); stutter += SweepMetrics.Stutter(r, sweep);
                var d = SweepMetrics.ActionDurations(r); duration += d.Total; runs += d.Runs;
                if (name == "duo-swap") swaps.Add(SweepMetrics.TargetLatency(r, scenario.DuoSwapSecond * settings!.TicksPerSecond));
                if (name == "standstill")
                {
                    int stop = r.Frames.First(f => f.Player.Z <= scenario.StandstillStop).Tick;
                    stops.Add(SweepMetrics.StandstillLatency(r, stop, settings!.TicksPerSecond));
                }
            }
            var orbit = PuppetSimulation.Run("orbit", temperament, seed, settings: settings, scenario: scenario);
            departures.AddRange(SweepMetrics.OrbitDepartures(orbit, sweep));
            gaps.Add(orbit.InterceptMeanDistance);
        }
        // No eligible target change is not a zero-latency response. Missing observations remain n/a.
        double? EligibleAverage(List<double?> samples) => samples.Any(x => x.HasValue) ? samples.Where(x => x.HasValue).Average(x => x!.Value) : null;
        double? CompleteAverage(List<double?> samples) => samples.All(x => x.HasValue) ? samples.Average(x => x!.Value) : null;
        return new(failures, stutter, EligibleAverage(swaps), CompleteAverage(stops), departures.Count == 0 ? 0 : departures.Average(), (double)duration / runs, tuning, null)
        { Contracts27 = contracts, Oscillations = oscillations, Patterns = patterns, Cycles = cycles, Dwell = dwell / 15.0, OrbitGap = gaps.Average(), NoDepartures = departures.Count == 0 };
    }
    internal static List<string> ContractViolations(PuppetTrajectory r, PuppetScenarioSettings s)
    {
        var failures = new List<string>();
        if (r.MaxSwitchesInWindow > s.SwitchLimit) failures.Add("진동 " + r.MaxSwitchesInWindow);
        if (r.PingPongs > 0) failures.Add("왕복 " + r.PingPongs);
        if (r.SimultaneousRatio >= s.SynchronizationLimit) failures.Add(FormattableString.Invariant($"군체 동기화 {r.SimultaneousRatio:R}"));
        if (r.MaxInterceptors > 1) failures.Add("겹치기 " + r.MaxInterceptors);
        if (r.MissedAttackWindows > 0) failures.Add("무반응 " + r.MissedAttackWindows);
        return failures;
    }

    public static int Run(string[] arguments, TextWriter output, TextWriter error, SweepSettings? sweep = null, PuppetScenarioSettings? scenario = null)
    {
        sweep ??= new(); scenario ??= new();
        string temperament = "none";
        var remaining = new List<string>();
        bool seenTemperament = false;
        for (int i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] != "--temperament") { remaining.Add(arguments[i]); continue; }
            if (seenTemperament || ++i == arguments.Length || !TemperamentPresets.Names.Contains(arguments[i]))
            { error.WriteLine("Expected one --temperament none|bold|cautious|persistent|whimsical|mixed"); return 2; }
            seenTemperament = true; temperament = arguments[i];
        }
        if (seenTemperament) output.WriteLine("temperament=" + temperament);
        if (!TryExpand(remaining.ToArray(), sweep.SweepMaxCombinations, out var combinations, out var reason)) { error.WriteLine(reason); return 2; }
        output.WriteLine("rank | forbidden | stutter | latency(i) | latency(ii) | latency(iii) | latency mean(tick) | action duration(tick) | harass cycle | attack dwell(tick) | orbit gap(m) | contracts27 | U7 F6");
        var baseline = Evaluate(new(), scenario, sweep, temperament);
        output.WriteLine(baseline.Print("baseline"));
        var rows = combinations.Select(c => Evaluate(c, scenario, sweep, temperament)).OrderBy(r => r.Rejection != null)
            .ThenBy(r => r.Failures != 0).ThenBy(r => r.Stutter).ThenBy(r => r.Mean ?? double.PositiveInfinity).ToArray();
        for (int i = 0; i < rows.Length; i++) output.WriteLine(rows[i].Print((i + 1).ToString(CultureInfo.InvariantCulture)));
        foreach (var entry in new[] { (Rank: "baseline", Value: baseline) }.Concat(rows.Select((row, i) => (Rank: (i + 1).ToString(CultureInfo.InvariantCulture), Value: row))))
        {
            if (entry.Value.Rejection != null) continue;
            foreach (string failure in entry.Value.Contracts27)
                output.WriteLine("rank " + entry.Rank + " 27조합: " + failure);
            foreach (string oscillation in entry.Value.Oscillations)
                output.WriteLine("rank " + entry.Rank + " 진동 " + oscillation);
            if (entry.Value.Failures != 0)
                output.WriteLine(entry.Rank + ": " + string.Join(", ", entry.Value.Patterns.Where(p => p.Value != 0).Select(p => p.Key + " " + p.Value)));
        }
        foreach (var entry in new[] { (Rank: "baseline", Value: baseline) }.Concat(rows.Select((row, i) => (Rank: (i + 1).ToString(CultureInfo.InvariantCulture), Value: row))))
            if (entry.Value.Rejection == null) output.WriteLine(entry.Rank + ": " + entry.Value.Cycles.Print);
        return 0;
    }
}
