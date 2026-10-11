namespace BehindTheCurtain.Core.Sim;

// Provisional measurement thresholds, arbitrarily chosen for tuning comparisons.
public sealed class SweepSettings
{
    public int SweepMaxCombinations { get; init; } = 64;
    public int StutterMinTicks { get; init; } = 6;
    public int StutterMaxTicks { get; init; } = 45;
    public double MovementThreshold { get; init; } = .01;
    public double PassbyReturnDistance { get; init; } = 1;
}

public static class SweepMetrics
{
    public static int Stutter(PuppetTrajectory trajectory, SweepSettings? settings = null)
    {
        settings ??= new(); int count = 0;
        var frames = trajectory.Frames;
        if (frames.Count == 0) return 0;
        foreach (var agent in frames[0].Puppets)
        {
            bool movingBefore = false, eligible = true; int stopped = 0;
            for (int i = 1; i < frames.Count; i++)
            {
                var current = frames[i].Puppets.Single(a => a.Id.Equals(agent.Id));
                var prior = frames[i - 1].Puppets.Single(a => a.Id.Equals(agent.Id));
                bool moving = PuppetSimulation.Distance(current.Position, prior.Position) > settings.MovementThreshold;
                if (moving)
                {
                    if (movingBefore && eligible && stopped >= settings.StutterMinTicks && stopped <= settings.StutterMaxTicks) count++;
                    movingBefore = true; stopped = 0; eligible = true;
                }
                else
                {
                    stopped += frames[i].Tick - frames[i - 1].Tick;
                    bool active = !frames[i].States.Any(s => s.Id.Equals(agent.Id) && s.Status != PuppetStatus.Active);
                    bool pressure = current.CurrentAction is ActionIds.Approach or ActionIds.Flank or ActionIds.Surround or ActionIds.Pressure
                        || current.CurrentAction == ActionIds.Harass && frames[i].HarassPhases.GetValueOrDefault(agent.Id.Value) is HarassPhase.Waiting or HarassPhase.Withdraw;
                    eligible &= active && pressure;
                }
            }
        }
        return count;
    }
    public static double? TargetLatency(PuppetTrajectory trajectory, int eventTick)
    {
        var before = trajectory.Frames.LastOrDefault(f => f.Tick < eventTick);
        if (before == null) return null;
        var end = trajectory.Frames.Last(); var delays = new List<double>();
        foreach (var a in before.Puppets.Where(a => a.Role != TargetRole.Healer))
        {
            if (Nullable.Equals(a.Target, end.Puppets.Single(p => p.Id.Equals(a.Id)).Target)) continue;
            var response = trajectory.Frames.First(f => f.Tick >= eventTick && !Nullable.Equals(f.Puppets.Single(p => p.Id.Equals(a.Id)).Target, a.Target));
            delays.Add(response.Tick - eventTick);
        }
        return delays.Count == 0 ? null : delays.Average();
    }
    public static double? StandstillLatency(PuppetTrajectory trajectory, int eventTick, int ticksPerSecond)
    {
        bool Changed(int i) => trajectory.Frames[i].Puppets.Any(a => a.CurrentAction != trajectory.Frames[i - 1].Puppets.Single(p => p.Id.Equals(a.Id)).CurrentAction);
        for (int i = 0; i < trajectory.Frames.Count; i++)
        {
            int tick = trajectory.Frames[i].Tick;
            if (tick < eventTick || trajectory.Frames.Last().Tick < tick + ticksPerSecond) continue;
            if (!Enumerable.Range(i + 1, trajectory.Frames.Count - i - 1).TakeWhile(j => trajectory.Frames[j].Tick <= tick + ticksPerSecond).Any(Changed)) return tick - eventTick;
        }
        return null;
    }
    public static double? PassbyLatency(PuppetTrajectory trajectory, Vec2 first, Vec2 second, double returnDistance = 1)
    {
        double Cross(Vec2 p) => (second.X - first.X) * (p.Z - first.Z) - (second.Z - first.Z) * (p.X - first.X);
        double initial = Cross(trajectory.Frames[0].Player);
        var crossing = trajectory.Frames.FirstOrDefault(f => Cross(f.Player) * initial <= 0);
        if (crossing == null) return null;
        foreach (var frame in trajectory.Frames.Where(f => f.Tick >= crossing.Tick))
            foreach (var a in frame.Puppets.Where(a => a.CurrentAction == ActionIds.Intercept))
            {
                double dx = second.X - first.X, dz = second.Z - first.Z;
                double fraction = Math.Clamp(((a.Position.X - first.X) * dx + (a.Position.Z - first.Z) * dz) / (dx * dx + dz * dz), 0, 1);
                if (PuppetSimulation.Distance(a.Position, new Vec2(first.X + fraction * dx, first.Z + fraction * dz)) <= returnDistance) return frame.Tick - crossing.Tick;
            }
        return null;
    }
    public static (int Total, int Runs) ActionDurations(PuppetTrajectory trajectory)
    {
        int total = 0, runs = 0;
        foreach (var a in trajectory.Frames[0].Puppets)
        {
            int start = trajectory.Frames[0].Tick; string? action = a.CurrentAction;
            foreach (var f in trajectory.Frames.Skip(1))
            {
                string? next = f.Puppets.Single(p => p.Id.Equals(a.Id)).CurrentAction;
                if (next == action) continue;
                total += f.Tick - start; runs++; start = f.Tick; action = next;
            }
            total += trajectory.Frames.Last().Tick + 1 - start; runs++;
        }
        return (total, runs);
    }
    internal static Dictionary<string, int> ForbiddenPatterns(PuppetTrajectory r, PuppetScenarioSettings s) => new()
    {
        ["도망"] = r.FleeTicks, ["왕복"] = r.PingPongs,
        ["진동"] = r.MaxSwitchesInWindow > s.SwitchLimit ? 1 : 0,
        ["군체 동기화"] = r.SimultaneousRatio >= s.SynchronizationLimit ? 1 : 0,
        ["겹치기"] = r.MaxInterceptors > 1 ? 1 : 0,
        ["무반응"] = r.MissedAttackWindows, ["위치 떨림"] = r.JitterFailures,
        ["비활동"] = r.InactivityFailures, ["포위 자리 겹침"] = r.SurroundReservationConflicts
    };
    internal sealed class HarassCycles
    {
        internal int Jabs, Waiting, Limited, Ordinary, Other;
        internal double? Completion => Jabs == 0 ? null : 100.0 * (Waiting + Limited) / Jabs;
        internal void Add(HarassCycles c)
        { Jabs += c.Jabs; Waiting += c.Waiting; Limited += c.Limited; Ordinary += c.Ordinary; Other += c.Other; }
        internal string Print => $"찌르기 {Jabs}, 대기 {Waiting}, 제한 {Limited}, 일반 공격 {Ordinary}, 기타 {Other}";
    }
    internal static HarassCycles HarassCycle(PuppetTrajectory trajectory)
    {
        var counts = new HarassCycles();
        if (trajectory.Frames.Count == 0) return counts;
        foreach (var agent in trajectory.Frames[0].Puppets)
        {
            bool pending = false, withdrew = false;
            foreach (var frame in trajectory.Frames)
            {
                var a = frame.Puppets.Single(p => p.Id.Equals(agent.Id));
                var phase = frame.HarassPhases.GetValueOrDefault(agent.Id.Value);
                bool jab = a.CurrentAction == ActionIds.Attack && phase == HarassPhase.Recover;
                if (pending)
                {
                    if (a.CurrentAction == ActionIds.Harass && phase == HarassPhase.Withdraw) withdrew = true;
                    else if (withdrew || jab || a.CurrentAction != ActionIds.Harass || phase != HarassPhase.Recover)
                    {
                        // Only completed cycles contribute to both numerator and denominator.
                        counts.Jabs++;
                        if (withdrew && phase == HarassPhase.Waiting && a.CurrentAction == ActionIds.Harass) counts.Waiting++;
                        else if (withdrew && (phase == HarassPhase.Strike || jab)) counts.Limited++;
                        else if (withdrew && a.CurrentAction == ActionIds.Attack && phase == null) counts.Ordinary++;
                        else counts.Other++;
                        pending = false; withdrew = false;
                    }
                }
                if (jab) pending = true;
            }
        }
        return counts;
    }
    internal static int AttackDwell(PuppetTrajectory trajectory, SweepSettings settings)
    {
        int longest = 0;
        if (trajectory.Frames.Count == 0) return longest;
        foreach (var agent in trajectory.Frames[0].Puppets)
        {
            int start = trajectory.Frames[0].Tick;
            for (int i = 0; i < trajectory.Frames.Count; i++)
            {
                var f = trajectory.Frames[i]; var a = f.Puppets.Single(p => p.Id.Equals(agent.Id));
                if (a.CurrentAction != ActionIds.Attack) { start = f.Tick + 1; continue; }
                if (i > 0)
                {
                    var before = trajectory.Frames[i - 1].Puppets.Single(p => p.Id.Equals(agent.Id));
                    if (before.CurrentAction != ActionIds.Attack || PuppetSimulation.Distance(a.Position, before.Position) > settings.MovementThreshold)
                        start = f.Tick;
                }
                longest = Math.Max(longest, f.Tick - start + 1);
            }
        }
        return longest;
    }
    internal static List<int> OrbitDepartures(PuppetTrajectory trajectory, SweepSettings settings)
    {
        var delays = new List<int>();
        if (trajectory.Frames.Count == 0) return delays;
        foreach (var agent in trajectory.Frames[0].Puppets)
        {
            bool entered = false; int? departed = null;
            foreach (var frame in trajectory.Frames)
            {
                var a = frame.Puppets.Single(p => p.Id.Equals(agent.Id));
                if (a.CurrentAction != ActionIds.Intercept) continue;
                var healer = frame.Puppets.Single(p => p.Id.Value == 13).Position;
                double dx = frame.Player.X - healer.X, dz = frame.Player.Z - healer.Z;
                double length = dx * dx + dz * dz;
                double fraction = length == 0 ? 0 : Math.Clamp(((a.Position.X - healer.X) * dx + (a.Position.Z - healer.Z) * dz) / length, 0, 1);
                bool inside = PuppetSimulation.Distance(a.Position, new Vec2(healer.X + fraction * dx, healer.Z + fraction * dz)) <= settings.PassbyReturnDistance;
                if (inside)
                {
                    if (departed.HasValue) { delays.Add(frame.Tick - departed.Value); departed = null; }
                    entered = true;
                }
                else if (entered && !departed.HasValue) departed = frame.Tick;
            }
            if (departed.HasValue) delays.Add(trajectory.Frames.Last().Tick - departed.Value);
        }
        return delays;
    }
    public static int Forbidden(PuppetTrajectory r, PuppetScenarioSettings s) =>
        r.FleeTicks + r.PingPongs + (r.MaxSwitchesInWindow > s.SwitchLimit ? 1 : 0)
        + (r.SimultaneousRatio >= s.SynchronizationLimit ? 1 : 0) + (r.MaxInterceptors > 1 ? 1 : 0)
        + r.MissedAttackWindows + r.JitterFailures + r.InactivityFailures + r.SurroundReservationConflicts;
}
