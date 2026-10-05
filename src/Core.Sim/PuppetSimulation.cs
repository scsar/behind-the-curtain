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
    public double StandstillStop { get; init; } = 4;
    public double PassbyHealerOffset { get; init; } = 3;
    public double OrbitRadius { get; init; } = 6;
    public int JitterMaxReversals { get; init; } = 3;
    public double JitterMovementThreshold { get; init; } = 0.001;
    public int JitterWindowTicks { get; init; } = 30;
    public int InterceptSettlingTicks { get; init; } = 30;
    public double InterceptQualityDistance { get; init; } = 1;
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
    public int InactivityLimitSeconds { get; init; } = 1;
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
    public IReadOnlyList<string> EquipmentTable { get; internal set; } = Array.Empty<string>();
    public List<PuppetFrame> Frames { get; } = new();
    /// <summary>Stationary non-attacking ticks past the limit for agents lacking their primary capability.</summary>
    public int InactivityFailures { get; internal set; }
    public int LongestInactiveTicks { get; internal set; }
    public Dictionary<int, int> MaxDirectionReversals { get; } = new();
    public int JitterFailures { get; internal set; }
    public int InterceptSettledTicks { get; internal set; }
    public int InterceptQualityTicks { get; internal set; }
    public double InterceptDistanceSum { get; internal set; }
    public double InterceptTravel { get; internal set; }
    public double InterceptQualityRatio => InterceptSettledTicks == 0 ? 0 : (double)InterceptQualityTicks / InterceptSettledTicks;
    public double InterceptMeanDistance => InterceptSettledTicks == 0 ? 0 : InterceptDistanceSum / InterceptSettledTicks;
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
    public static readonly string[] Names = { "approach", "circle", "retreat", "healer", "standstill", "orbit", "passby" };

    public static PuppetTrajectory Run(string name, ulong seed = 1, IntentionSettings? intentions = null,
        PuppetSettings? settings = null, PuppetScenarioSettings? scenario = null)
        => Run(name, seed, intentions, settings, scenario, "none", new DirectorProfile());

    public static PuppetTrajectory Run(string name, ulong seed, IntentionSettings? intentions,
        PuppetSettings? settings, PuppetScenarioSettings? scenario, string stress, DirectorProfile? profile = null, string loadout = "presets", bool explicitPresets = false)
        => Run(name, seed, intentions, settings, scenario, stress, false, profile, loadout, explicitPresets);

    public static PuppetTrajectory Run(string name, ulong seed, IntentionSettings? intentions,
        PuppetSettings? settings, PuppetScenarioSettings? scenario, string stress, bool cast, DirectorProfile? profile = null, string loadout = "presets", bool explicitPresets = false)
    {
        if (loadout is not ("presets" or "mixed" or "noprotect")) throw new ArgumentException("Unknown loadout", nameof(loadout));
        if (stress is not ("none" or "tension" or "mixed")) throw new ArgumentException("Unknown stress", nameof(stress));
        if (!Names.Contains(name)) throw new ArgumentException("Unknown puppet scenario", nameof(name));
        scenario ??= new PuppetScenarioSettings();
        settings ??= new PuppetSettings(arenaHalfSize: scenario.ArenaHalfSize);
        Expedition.TryCreate(new ExpeditionSettings(intentions: intentions), new RuleTacticSelector(), out var expedition);
        expedition!.TryStartEncounter(out _);
        if (name is "healer" or "standstill" or "orbit" or "passby")
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
        if (cast && !DirectorCasting.Assign(world).Success) throw new InvalidOperationException("Casting failed");
        var brain = PuppetBrain.Create(world, settings, new DeterministicRandom(seed), profile ?? new DirectorProfile()).Value;
        var result = new PuppetTrajectory { HasHealerThreat = world.GetBlackboard(Side.Puppets).Value.GetFact<AgentId>(TargetFactIds.HealerThreat).Success };
        result.EquipmentTable = world.GetAgents().Where(a => a.Side == Side.Puppets).Select(a =>
        {
            var equipment = world.GetLoadout(a.Id).Value;
            return FormattableString.Invariant($"{a.Id.Value}:{a.Role} equipment={string.Join(",", equipment.EquipmentIds)} capabilities={string.Join(",", equipment.Capabilities)} attackRange={equipment.GetAttackRange(settings):F2} moveSpeedScale={equipment.MoveSpeedScale:F2} goal={RoleContract.ForRole(a.Role).Goal}");
        }).ToArray();
        var inactive = new Dictionary<int, int>();
        var outside = new Dictionary<int, int>();
        var previousMoves = new Dictionary<int, Vec2>();
        var reversals = new Dictionary<int, Queue<int>>();
        var interceptAge = new Dictionary<int, int>();
        var histories = new Dictionary<int, List<string?>>();
        int residence = 0, noAttack = 0;
        for (int tick = 0; tick <= scenario.Seconds * settings.TicksPerSecond; tick++)
        {
            double time = (double)tick / settings.TicksPerSecond;
            Vec2 player = name switch
            {
                "standstill" => new Vec2(0, Math.Max(scenario.StandstillStop, scenario.Far - scenario.PlayerSpeed * time)),
                "passby" => new Vec2(scenario.HealerStart.X, Math.Max(scenario.HealerStart.Z - scenario.PassbyHealerOffset, scenario.Far - scenario.PlayerSpeed * time)),
                "orbit" => new Vec2(scenario.HealerStart.X + scenario.OrbitRadius * Math.Sin(time * scenario.AngularSpeed),
                    scenario.HealerStart.Z + scenario.OrbitRadius * Math.Cos(time * scenario.AngularSpeed)),
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
            if (name == "orbit") world.UpdatePosition(new AgentId(13), scenario.HealerStart, new Vec2(0, 1));
            var after = world.GetAgents().Where(a => a.Side == Side.Puppets).ToArray();
            result.Frames.Add(new PuppetFrame(tick, player, after) { States = world.GetPuppetStates().ToArray(),
                TuneTarget = brain.TuneTarget, TuneAmount = brain.TuneAmount, Budget = brain.CurrentTuneBudgetPerSecond });
            int changed = 0;
            for (int i = 0; i < after.Length; i++)
            {
                var a = after[i]; int id = a.Id.Value;
                var move = new Vec2(a.Position.X - before[i].Position.X, a.Position.Z - before[i].Position.Z);
                if (!reversals.ContainsKey(id)) { reversals[id] = new(); result.MaxDirectionReversals[id] = 0; }
                if (previousMoves.TryGetValue(id, out var priorMove)
                    && Distance(move, new Vec2(0, 0)) > scenario.JitterMovementThreshold
                    && Distance(priorMove, new Vec2(0, 0)) > scenario.JitterMovementThreshold
                    && move.X * priorMove.X + move.Z * priorMove.Z < 0) reversals[id].Enqueue(tick);
                previousMoves[id] = move;
                while (reversals[id].Count > 0 && reversals[id].Peek() <= tick - scenario.JitterWindowTicks) reversals[id].Dequeue();
                result.MaxDirectionReversals[id] = Math.Max(result.MaxDirectionReversals[id], reversals[id].Count);
                if (reversals[id].Count > scenario.JitterMaxReversals) result.JitterFailures++;
                interceptAge[id] = a.CurrentAction == ActionIds.Intercept ? interceptAge.GetValueOrDefault(id) + 1 : 0;
                if (interceptAge[id] > scenario.InterceptSettlingTicks)
                {
                    var healer = after.Single(h => h.Id.Value == 13);
                    double gap = Distance(a.Position, InterceptGeometry.ClosestPoint(a.Position, healer.Position, player, settings));
                    result.InterceptSettledTicks++;
                    if (gap <= scenario.InterceptQualityDistance) result.InterceptQualityTicks++;
                    result.InterceptDistanceSum += gap;
                    result.InterceptTravel += Distance(before[i].Position, a.Position);
                }
                if (tick == 0)
                {
                    result.Switches[id] = new(); histories[id] = new() { a.CurrentAction };
                    inactive[id] = 0; outside[id] = 0; result.LongestOutsideBand[id] = 0;
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
                        ((h[^1] is ActionIds.Approach or ActionIds.Pressure && h[^2] is ActionIds.Hold or ActionIds.KeepDistance)
                        || (h[^2] is ActionIds.Approach or ActionIds.Pressure && h[^1] is ActionIds.Hold or ActionIds.KeepDistance))) result.PingPongs++;
                }
                int switches = result.Switches[id].Count(t => t > tick - scenario.SwitchWindowSeconds * settings.TicksPerSecond);
                result.MaxSwitchesInWindow = Math.Max(result.MaxSwitchesInWindow, switches);
                if (!world.CanAct(a.Id)) { inactive[id] = 0; continue; }
                double oldDistance = Distance(before[i].Position, player), distance = Distance(a.Position, player);
                bool unableToPerformPrimary = a.Role == TargetRole.Guard && !world.GetLoadout(a.Id).Value.HasCapability(CapabilityTag.Protect);
                inactive[id] = unableToPerformPrimary && distance <= settings.ThreatRadius && a.CurrentAction != ActionIds.Waver && a.CurrentAction != ActionIds.Attack
                    && Distance(before[i].Position, a.Position) <= scenario.Epsilon ? inactive[id] + 1 : 0;
                result.LongestInactiveTicks = Math.Max(result.LongestInactiveTicks, inactive[id]);
                if (inactive[id] > scenario.InactivityLimitSeconds * settings.TicksPerSecond) result.InactivityFailures++;
                var keepScore = brain.GetDecision(a.Id).Value.Selection.Scores.Single(s => s.ActionId == ActionIds.KeepDistance);
                bool keepEnabled = keepScore.IntentionMultiplier > 0 &&
                    (settings.GetRoleWeight(a.Role, ActionIds.KeepDistance) > 0 || keepScore.Product > 0);
                if (a.CurrentAction != ActionIds.Intercept && !keepEnabled && oldDistance <= settings.ThreatRadius && distance > oldDistance + scenario.Epsilon
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
            // Passed interceptors prioritize recovery over a stationary attack.
            bool inRange = before.Any(a => world.CanAct(a.Id) && after.Single(b => b.Id.Equals(a.Id)).CurrentAction != ActionIds.Intercept && after.Single(b => b.Id.Equals(a.Id)).CurrentAction != ActionIds.Waver && Distance(a.Position, player) <= world.GetAttackRange(a.Id, settings).Value);
            bool attacking = after.Any(a => a.CurrentAction == ActionIds.Attack && a.Target.HasValue
                && Distance(a.Position, player) <= world.GetAttackRange(a.Id, settings).Value);
            if (attacking) result.AttackTicks++;
            residence = inRange ? residence + 1 : 0;
            noAttack = inRange && !attacking ? noAttack + 1 : 0;
            result.LongestAttackResidence = Math.Max(result.LongestAttackResidence, residence);
            if (noAttack > settings.TicksPerSecond) result.MissedAttackWindows++;
        }
        result.Events = world.StateEvents.ToArray();
        return result;

        void Add(int id, TargetRole role, Vec2 position, Side side = Side.Puppets)
        {
            var agent = new AgentState(new AgentId(id), side, side == Side.Players, role, position, new Vec2(0, 1), scenario.AgentRadius);
            var initial = new PuppetInitialState(stress == "mixed" && id == 13 ? PuppetKind.Humanized : PuppetKind.Puppet);
            if (side == Side.Players || (loadout == "presets" && !explicitPresets)) world.Add(agent, initial);
            else
            {
                var equipment = loadout == "presets" || id == 13 ? LoadoutPresets.ForRole(role)
                    : loadout == "noprotect" ? Loadout.Create(new[] { "weapon.melee" }).Value
                    : Loadout.Create(id == 10 ? new[] { "weapon.melee", "armor.heavyShield" }
                        : id == 11 ? new[] { "weapon.melee" } : new[] { "weapon.spear" }).Value;
                world.Add(agent, initial, equipment);
            }
        }
    }

    public static double Distance(Vec2 a, Vec2 b) => Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z));

    public static void Print(PuppetTrajectory result, TextWriter output, PuppetScenarioSettings? settings = null)
    {
        settings ??= new PuppetScenarioSettings();
        foreach (var row in result.EquipmentTable) output.WriteLine(row);
        foreach (var frame in result.Frames.Where(f => f.Tick % settings.SampleTicks == 0))
            output.WriteLine(FormattableString.Invariant($"tick={frame.Tick} player=({frame.Player.X:F2},{frame.Player.Z:F2}) ") +
                string.Join(" ", frame.Puppets.Select(a => FormattableString.Invariant(
                    $"{a.Id.Value}:{a.Role}/{a.CurrentAction}({a.Position.X:F2},{a.Position.Z:F2})"))) +
                string.Join(" ", frame.States.Select(s => $" {s.Id.Value}:T={s.Tension:F3}/P={s.Posture:F1}/{s.Status}")) +
                $" tune={frame.TuneTarget?.Value.ToString() ?? "-"} amount={frame.TuneAmount:F4} budget={frame.Budget:F3}");
        foreach (var e in result.Events)
            output.WriteLine(FormattableString.Invariant($"event tick={e.Tick} agent={e.AgentId.Value} {e.Kind} {e.Cause.Kind} {e.Before.Status}->{e.After.Status} value={e.ValueId} T={e.Before.Tension:F4}->{e.After.Tension:F4} P={e.Before.Posture:F2}->{e.After.Posture:F2} H={e.Before.Health:F2}->{e.After.Health:F2} reclaim={e.ReclaimedAmount:F3} budget={e.BudgetAfter:F3}"));
        output.WriteLine(FormattableString.Invariant(
            $"inactive={result.InactivityFailures} longestInactive={result.LongestInactiveTicks} flee={result.FleeTicks} switches={string.Join(",", result.Switches.Select(p => p.Key + ":" + p.Value.Count))} simultaneous={result.SimultaneousRatio:P1} maxIntercept={result.MaxInterceptors} intercept={result.InterceptRatio:P1} attackTicks={result.AttackTicks} residence={result.LongestAttackResidence} missedAttack={result.MissedAttackWindows} maxSwitches/3s={result.MaxSwitchesInWindow} pingPong={result.PingPongs} jitter={result.JitterFailures} maxReversals/1s={string.Join(",", result.MaxDirectionReversals.Select(p => p.Key + ":" + p.Value))}"));
        output.WriteLine(result.InterceptAgentTicks == 0 ? "interceptQuality ticks=0 n/a(막아서기 없음)" :
            FormattableString.Invariant($"interceptQuality ticks={result.InterceptAgentTicks} settled={result.InterceptSettledTicks} within1m={result.InterceptQualityRatio:P1} mean={result.InterceptMeanDistance:F3}m travel={result.InterceptTravel:F3}m"));
    }
}
