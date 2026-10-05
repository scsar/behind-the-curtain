using BehindTheCurtain.Core;

namespace BehindTheCurtain.Core.Sim;

/// <summary>Arbitrarily chosen prototype paths and contract measurement tolerances.</summary>
public sealed class PuppetScenarioSettings
{
    // Provisional stress inputs, chosen arbitrarily for this experiment.
    public int AttackerStressSecond { get; init; } = 2;
    public int GuardStressSecond { get; init; } = 4;
    public int HumanizedStressSecond { get; init; } = 3;
    public int BreakStressSecond { get; init; } = 5;
    public double AttackerTensionLoss { get; init; } = 0.8;
    public double GuardTensionLoss { get; init; } = 0.6;
    public double HumanizedTensionLoss { get; init; } = 1;
    public double BreakDamage { get; init; } = 100;
    public int Seconds { get; init; } = 10;
    public double ArenaHalfSize { get; init; } = 30;
    public double AgentRadius { get; init; } = 0.3;
    public Vec2 FirstGuardStart { get; init; } = new Vec2(-1.5, 0);
    public Vec2 SecondGuardStart { get; init; } = new Vec2(1.5, 0);
    public Vec2 AttackerStart { get; init; } = new Vec2(-2, -1);
    public Vec2 HealerStart { get; init; } = new Vec2(0, -3);
    public int SampleTicks { get; init; } = 15;
    public double Near { get; init; } = 3;
    public double Far { get; init; } = 12;
    public double PlayerSpeed { get; init; } = 5;
    public double CircleRadius { get; init; } = 4;
    public double AngularSpeed { get; init; } = 0.5;
    public double SpreadAllowance { get; init; } = 0.3;
    public double Epsilon { get; init; } = 1e-8;
    public int SwitchWindowSeconds { get; init; } = 3;
    public int SwitchLimit { get; init; } = 3;
    public double SynchronizationLimit { get; init; } = 0.2;
    public double InterceptLimit { get; init; } = 0.2;
}

public sealed record PuppetFrame(int Tick, Vec2 Player, AgentState[] Puppets)
{
    public PuppetState[] States { get; init; } = Array.Empty<PuppetState>();
    public AgentId? TuneTarget { get; init; }
    public double TuneAmount { get; init; }
    public double Budget { get; init; }
}

public sealed class PuppetTrajectory
{
    public IReadOnlyList<PuppetStateEvent> Events { get; internal set; } = Array.Empty<PuppetStateEvent>();
    public List<PuppetFrame> Frames { get; } = new();
    public int FleeTicks { get; internal set; }
    public Dictionary<int, List<int>> Switches { get; } = new();
    public int ChangedTicks { get; internal set; }
    public int SimultaneousTicks { get; internal set; }
    public double SimultaneousRatio => ChangedTicks == 0 ? 0 : (double)SimultaneousTicks / ChangedTicks;
    public bool HasHealerThreat { get; internal set; }
    public int MaxInterceptors { get; internal set; }
    public int InterceptAgentTicks { get; internal set; }
    public double InterceptRatio => (double)InterceptAgentTicks / (Frames.Count * Frames[0].Puppets.Length);
    public int MissedAttackWindows { get; internal set; }
    public int AttackTicks { get; internal set; }
    public int LongestAttackResidence { get; internal set; }
    public int PingPongs { get; internal set; }
    public int MaxSwitchesInWindow { get; internal set; }
    public Dictionary<int, int> LongestOutsideBand { get; } = new();
}

public static class PuppetSimulation
{
    public static readonly string[] Names = { "approach", "circle", "retreat", "healer" };

    public static PuppetTrajectory Run(string name, ulong seed = 1, IntentionSettings? intentions = null,
        PuppetSettings? settings = null, PuppetScenarioSettings? scenario = null)
        => Run(name, seed, intentions, settings, scenario, "none", new DirectorProfile());

    public static PuppetTrajectory Run(string name, ulong seed, IntentionSettings? intentions,
        PuppetSettings? settings, PuppetScenarioSettings? scenario, string stress, DirectorProfile? profile = null)
    {
        if (stress is not ("none" or "tension" or "mixed")) throw new ArgumentException("Unknown stress", nameof(stress));
        if (!Names.Contains(name)) throw new ArgumentException("Unknown puppet scenario", nameof(name));
        scenario ??= new PuppetScenarioSettings();
        settings ??= new PuppetSettings(arenaHalfSize: scenario.ArenaHalfSize);
        Expedition.TryCreate(new ExpeditionSettings(intentions: intentions), new RuleTacticSelector(), out var expedition);
        expedition!.TryStartEncounter(out _);
        if (name == "healer")
        {
            CombatObserver.TryCreate(new ObserverSettings(), out var observer);
            observer!.Observe(new AttackEvent(0, TargetRole.Healer, AttackRange.Melee, 0, new AgentId(1), true));
            expedition.EndEncounterWithIndividuals(observer.EndEncounterWithIndividuals());
            expedition.TryStartEncounter(out _);
        }
        var world = expedition.EncounterWorld!;
        Add(1, TargetRole.Attacker, new Vec2(0, scenario.Far), Side.Players);
        Add(10, TargetRole.Guard, scenario.FirstGuardStart);
        Add(11, TargetRole.Guard, scenario.SecondGuardStart);
        Add(12, TargetRole.Attacker, scenario.AttackerStart);
        Add(13, TargetRole.Healer, scenario.HealerStart);
        var brain = PuppetBrain.Create(world, settings, new DeterministicRandom(seed), profile ?? new DirectorProfile()).Value;
        var result = new PuppetTrajectory { HasHealerThreat = world.GetBlackboard(Side.Puppets).Value.GetFact<AgentId>(TargetFactIds.HealerThreat).Success };
        var outside = new Dictionary<int, int>();
        var histories = new Dictionary<int, List<string?>>();
        int residence = 0, noAttack = 0;
        for (int tick = 0; tick <= scenario.Seconds * settings.TicksPerSecond; tick++)
        {
            double time = (double)tick / settings.TicksPerSecond;
            Vec2 player = name switch
            {
                "approach" => new Vec2(0, Math.Max(scenario.Near, scenario.Far - scenario.PlayerSpeed * time)),
                "retreat" => new Vec2(0, Math.Min(scenario.Far, scenario.Near + scenario.PlayerSpeed * time)),
                "circle" => new Vec2(scenario.CircleRadius * Math.Sin(time * scenario.AngularSpeed),
                    scenario.CircleRadius * Math.Cos(time * scenario.AngularSpeed)),
                _ => new Vec2(scenario.CircleRadius * Math.Sin(Math.Min(Math.PI, time * scenario.AngularSpeed)),
                    scenario.CircleRadius * Math.Cos(Math.Min(Math.PI, time * scenario.AngularSpeed)))
            };
            world.UpdatePosition(new AgentId(1), player, new Vec2(0, 1));
            if (stress == "tension")
            {
                if (tick == scenario.AttackerStressSecond * settings.TicksPerSecond) world.ReduceTension(new AgentId(12), scenario.AttackerTensionLoss, tick);
                if (tick == scenario.GuardStressSecond * settings.TicksPerSecond)
                    foreach (int id in new[] { 10, 11 }) world.ReduceTension(new AgentId(id), scenario.GuardTensionLoss, tick);
            }
            if (stress == "mixed")
            {
                if (tick == scenario.HumanizedStressSecond * settings.TicksPerSecond) world.ReduceTension(new AgentId(13), scenario.HumanizedTensionLoss, tick);
                if (tick == scenario.BreakStressSecond * settings.TicksPerSecond) world.ApplyDamage(new AgentId(12), scenario.BreakDamage, 0, tick);
            }
            var before = world.GetAgents().Where(a => a.Side == Side.Puppets).ToArray();
            if (brain.Tick(tick) != AiFailure.None) throw new InvalidOperationException("Brain tick failed");
            var after = world.GetAgents().Where(a => a.Side == Side.Puppets).ToArray();
            result.Frames.Add(new PuppetFrame(tick, player, after) { States = world.GetPuppetStates().ToArray(),
                TuneTarget = brain.TuneTarget, TuneAmount = brain.TuneAmount, Budget = brain.CurrentTuneBudgetPerSecond });
            int changed = 0;
            for (int i = 0; i < after.Length; i++)
            {
                var a = after[i]; int id = a.Id.Value;
                if (tick == 0)
                {
                    result.Switches[id] = new(); histories[id] = new() { a.CurrentAction };
                    outside[id] = 0; result.LongestOutsideBand[id] = 0;
                }
                else if (world.CanAct(a.Id) && a.CurrentAction != before[i].CurrentAction)
                {
                    changed++;
                    // Resuming after Groggy is a new action, not an action-to-action oscillation.
                    bool wasActive = result.Frames[tick - 1].States.Single(s => s.Id.Equals(a.Id)).Status == PuppetStatus.Active;
                    if (wasActive) result.Switches[id].Add(tick);
                    else histories[id].Clear();
                    histories[id].Add(a.CurrentAction);
                    var h = histories[id];
                    if (h.Count >= 3 && h[^1] == h[^3] &&
                        ((h[^1] == ActionIds.Approach && h[^2] is ActionIds.Hold or ActionIds.KeepDistance)
                        || (h[^2] == ActionIds.Approach && h[^1] is ActionIds.Hold or ActionIds.KeepDistance))) result.PingPongs++;
                }
                int switches = result.Switches[id].Count(t => t > tick - scenario.SwitchWindowSeconds * settings.TicksPerSecond);
                result.MaxSwitchesInWindow = Math.Max(result.MaxSwitchesInWindow, switches);
                if (!world.CanAct(a.Id)) continue;
                double oldDistance = Distance(before[i].Position, player), distance = Distance(a.Position, player);
                var keepScore = brain.GetDecision(a.Id).Value.Selection.Scores.Single(s => s.ActionId == ActionIds.KeepDistance);
                bool keepEnabled = keepScore.IntentionMultiplier > 0 &&
                    (settings.GetRoleWeight(a.Role, ActionIds.KeepDistance) > 0 || keepScore.Product > 0);
                if (!keepEnabled && oldDistance <= settings.ThreatRadius && distance > oldDistance + scenario.Epsilon
                    && !(a.CurrentAction == ActionIds.Spread && distance - oldDistance <= scenario.SpreadAllowance)) result.FleeTicks++;
                if (keepEnabled)
                {
                    outside[id] = distance < settings.KeepMin - scenario.Epsilon || distance > settings.KeepMax + scenario.Epsilon
                        ? outside[id] + 1 : 0;
                    result.LongestOutsideBand[id] = Math.Max(result.LongestOutsideBand[id], outside[id]);
                }
            }
            if (changed > 0) result.ChangedTicks++;
            if (changed >= 2) result.SimultaneousTicks++;
            int interceptors = after.Count(a => a.CurrentAction == ActionIds.Intercept);
            result.MaxInterceptors = Math.Max(result.MaxInterceptors, interceptors);
            result.InterceptAgentTicks += interceptors;
            bool inRange = before.Any(a => world.CanAct(a.Id) && after.Single(b => b.Id.Equals(a.Id)).CurrentAction != ActionIds.Waver && Distance(a.Position, player) <= settings.AttackRange);
            bool attacking = after.Any(a => a.CurrentAction == ActionIds.Attack && a.Target.HasValue
                && Distance(a.Position, player) <= settings.AttackRange);
            if (attacking) result.AttackTicks++;
            residence = inRange ? residence + 1 : 0;
            noAttack = inRange && !attacking ? noAttack + 1 : 0;
            result.LongestAttackResidence = Math.Max(result.LongestAttackResidence, residence);
            if (noAttack > settings.TicksPerSecond) result.MissedAttackWindows++;
        }
        result.Events = world.StateEvents.ToArray();
        return result;

        void Add(int id, TargetRole role, Vec2 position, Side side = Side.Puppets) =>
            world.Add(new AgentState(new AgentId(id), side, side == Side.Players, role, position, new Vec2(0, 1), scenario.AgentRadius), new PuppetInitialState(stress == "mixed" && id == 13 ? PuppetKind.Humanized : PuppetKind.Puppet));
    }

    public static double Distance(Vec2 a, Vec2 b) => Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z));

    public static void Print(PuppetTrajectory result, TextWriter output, PuppetScenarioSettings? settings = null)
    {
        settings ??= new PuppetScenarioSettings();
        foreach (var frame in result.Frames.Where(f => f.Tick % settings.SampleTicks == 0))
            output.WriteLine(FormattableString.Invariant($"tick={frame.Tick} player=({frame.Player.X:F2},{frame.Player.Z:F2}) ") +
                string.Join(" ", frame.Puppets.Select(a => FormattableString.Invariant(
                    $"{a.Id.Value}:{a.Role}/{a.CurrentAction}({a.Position.X:F2},{a.Position.Z:F2})"))) +
                string.Join(" ", frame.States.Select(s => $" {s.Id.Value}:T={s.Tension:F3}/P={s.Posture:F1}/{s.Status}")) +
                $" tune={frame.TuneTarget?.Value.ToString() ?? "-"} amount={frame.TuneAmount:F4} budget={frame.Budget:F3}");
        foreach (var e in result.Events)
            output.WriteLine(FormattableString.Invariant($"event tick={e.Tick} agent={e.AgentId.Value} {e.Kind} {e.Cause.Kind} {e.Before.Status}->{e.After.Status} value={e.ValueId} T={e.Before.Tension:F4}->{e.After.Tension:F4} P={e.Before.Posture:F2}->{e.After.Posture:F2} H={e.Before.Health:F2}->{e.After.Health:F2} reclaim={e.ReclaimedAmount:F3} budget={e.BudgetAfter:F3}"));
        output.WriteLine(FormattableString.Invariant(
            $"flee={result.FleeTicks} switches={string.Join(",", result.Switches.Select(p => p.Key + ":" + p.Value.Count))} simultaneous={result.SimultaneousRatio:P1} maxIntercept={result.MaxInterceptors} intercept={result.InterceptRatio:P1} attackTicks={result.AttackTicks} residence={result.LongestAttackResidence} missedAttack={result.MissedAttackWindows} maxSwitches/3s={result.MaxSwitchesInWindow} pingPong={result.PingPongs}"));
    }
}
