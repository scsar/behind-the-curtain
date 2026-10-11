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
    public static int Forbidden(PuppetTrajectory r, PuppetScenarioSettings s) =>
        r.FleeTicks + r.PingPongs + (r.MaxSwitchesInWindow > s.SwitchLimit ? 1 : 0)
        + (r.SimultaneousRatio >= s.SynchronizationLimit ? 1 : 0) + (r.MaxInterceptors > 1 ? 1 : 0)
        + r.MissedAttackWindows + r.JitterFailures + r.InactivityFailures + r.SurroundReservationConflicts;
}
