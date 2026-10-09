using System;
using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    public sealed class PuppetDecision
    {
        internal PuppetDecision(AgentId id, UtilitySelection selection, Vec2 goal, AgentId? target)
        { AgentId = id; Selection = selection; Goal = goal; Target = target; }
        public AgentId AgentId { get; }
        public TargetRole Role { get; internal set; }
        public RoleContract Contract => RoleContract.ForRole(Role);
        public int ContractActionIndex => Contract.IndexOf(Selection.ActionId);
        public ContractActionKind ContractActionKind => Contract.Classify(Selection.ActionId);
        public UtilitySelection Selection { get; }
        /// <summary>Arena-clamped destination used for this tick, including stationary attacks.</summary>
        public Vec2 Goal { get; }
        public AgentId? Target { get; }
        public HarassPhase? HarassPhase { get; internal set; }
        public int? SurroundSlot { get; internal set; }
        public DecisionCause? Cause { get; internal set; }
    }

    /// <summary>
    /// One brain per encounter. Create after populating World to capture formation positions.
    /// Tick once per consecutive simulation tick; the first tick may be any signed integer.
    /// All puppets decide before any move. Players (including bots) remain caller-owned.
    /// </summary>
    public sealed class PuppetBrain
    {
        private sealed class Puppet
        {
            internal Puppet(AgentId id, PuppetActions actions, UtilitySelector selector)
            { Id = id; Actions = actions; Selector = selector; }
            internal AgentId Id { get; }
            internal PuppetActions Actions { get; }
            internal UtilitySelector Selector { get; }
        }
        private readonly World world;
        private readonly PuppetSettings settings;
        private readonly List<Puppet> puppets;
        private readonly Dictionary<AgentId, PuppetDecision> decisions = new Dictionary<AgentId, PuppetDecision>();
        private DirectorProfile profile = new DirectorProfile();
        private readonly Dictionary<AgentId, Vec2> formation = new Dictionary<AgentId, Vec2>();
        private int eventCursor;
        /// <summary>Budget after processing transitions at the latest successful tick.</summary>
        public double CurrentTuneBudgetPerSecond { get; private set; }
        /// <summary>Tuning occurs after decisions and movement; this is the latest tick's target.</summary>
        public AgentId? TuneTarget { get; private set; }
        public double TuneAmount { get; private set; }
        public long? TuneTick { get; private set; }
        private long? lastTick;
        private long firstTick;
        private long participationVersion;
        private PuppetBrain(World world, PuppetSettings settings, List<Puppet> puppets)
        { this.world = world; this.settings = settings; this.puppets = puppets; }

        public static AiResult<PuppetBrain> Create(World world, PuppetSettings settings, DeterministicRandom random)
            => Create(world, settings, random, new DirectorProfile());

        public static AiResult<PuppetBrain> Create(World world, PuppetSettings settings, DeterministicRandom random, DirectorProfile profile)
        {
            if (profile == null || !profile.IsValid) return AiResult<PuppetBrain>.Fail(AiFailure.InvalidSettings);
            if (world == null) return AiResult<PuppetBrain>.Fail(AiFailure.InvalidValue);
            if (settings == null || !settings.IsValid || random == null || world.StateSettings == null || !world.StateSettings.IsValid)
                return AiResult<PuppetBrain>.Fail(AiFailure.InvalidSettings);
            var puppets = new List<Puppet>();
            ulong encounterSeed = (ulong)(random.NextDouble() * uint.MaxValue);
            foreach (var agent in world.GetAgents())
            {
                if (agent.Side != Side.Puppets) continue;
                var actions = new PuppetActions(settings, agent.Position);
                var selector = UtilitySelector.Create(world, agent.Id, actions.Create(agent.Role, new DeterministicRandom(encounterSeed ^ (ulong)agent.Id.Value), profile, world.GetLoadout(agent.Id).Value),
                    new UtilitySettings(1, settings.PersistenceBonus), random);
                if (!selector.Success) return AiResult<PuppetBrain>.Fail(selector.Failure);
                puppets.Add(new Puppet(agent.Id, actions, selector.Value));
            }
            world.SetStateTickRate(settings.TicksPerSecond);
            var brain = new PuppetBrain(world, settings, puppets) { profile = profile, CurrentTuneBudgetPerSecond = profile.TuneBudgetPerSecond };
            foreach (var agent in world.GetAgents()) if (agent.Side == Side.Puppets) brain.formation[agent.Id] = agent.Position;
            return AiResult<PuppetBrain>.Ok(brain);
        }

        /// <summary>No decision exists before the first successful tick.</summary>
        public AiResult<PuppetDecision> GetDecision(AgentId id) => world.CanAct(id) && decisions.TryGetValue(id, out var value)
            ? AiResult<PuppetDecision>.Ok(value) : AiResult<PuppetDecision>.Fail(AiFailure.AgentNotFound);

        public AiFailure Tick(long tick)
        {
            if (lastTick.HasValue && (lastTick.Value == long.MaxValue || tick != lastTick.Value + 1))
                return AiFailure.InvalidTick;
            if (!lastTick.HasValue) firstTick = tick;
            var advanced = world.AdvancePuppetStates(tick, settings.TicksPerSecond);
            if (advanced != AiFailure.None) return advanced;
            Reclaim();
            bool participationChanged = participationVersion != world.ParticipationVersion;
            var pending = new List<PuppetDecision>();
            int ordinal = 0;
            foreach (var puppet in puppets)
            {
                int offset = (int)((long)ordinal++ * settings.ReevaluationIntervalTicks / puppets.Count);
                if (!world.CanAct(puppet.Id)) { decisions.Remove(puppet.Id); puppet.Actions.ResetBasicActions(); continue; }
                // Only ally-dependent decisions need immediate invalidation. Unrelated approach/attack
                // decisions retain their staggered cadence when another puppet leaves or reconnects.
                bool dependsOnAllies = decisions.TryGetValue(puppet.Id, out var prior) &&
                    (prior.Selection.ActionId == ActionIds.Surround || prior.Selection.ActionId == ActionIds.Intercept || prior.Selection.ActionId == ActionIds.Spread ||
                     prior.Selection.ActionId == ActionIds.KeepDistance);
                bool evaluate = (participationChanged && dependsOnAllies) || !decisions.ContainsKey(puppet.Id) ||
                    unchecked((ulong)tick - (ulong)firstTick) % (ulong)settings.ReevaluationIntervalTicks == (ulong)offset;
                var agent = world.Get(puppet.Id).Value;
                var context = new UtilityContext(world, agent, world.GetBlackboard(Side.Puppets).Value, tick);
                var selected = evaluate ? puppet.Selector.Select(tick)
                    : AiResult<UtilitySelection>.Ok(new UtilitySelection(decisions[puppet.Id].Selection.ActionId,
                        decisions[puppet.Id].Selection.EvaluatedAtTick, false, decisions[puppet.Id].Selection.Scores));
                if (!selected.Success) return selected.Failure;
                if (selected.Value.ActionId != ActionIds.Waver && world.GetPuppetState(agent.Id).Value.Tension > 0
                    && puppet.Actions.IsCommitted(context) && puppet.Actions.CommittedAction != null)
                    selected = AiResult<UtilitySelection>.Ok(new UtilitySelection(puppet.Actions.CommittedAction,
                        selected.Value.EvaluatedAtTick, selected.Value.Reevaluated, selected.Value.Scores));
                puppet.Actions.Reserve(context, selected.Value.ActionId);
                var resolved = puppet.Actions.Resolve(context, selected.Value.ActionId);
                if (puppet.Actions.HarassAttack)
                    selected = AiResult<UtilitySelection>.Ok(new UtilitySelection(ActionIds.Attack,
                        selected.Value.EvaluatedAtTick, selected.Value.Reevaluated, selected.Value.Scores));
                var decision = new PuppetDecision(puppet.Id, selected.Value, Clamp(resolved.Goal), resolved.Target) { Role = agent.Role, HarassPhase = puppet.Actions.Phase, SurroundSlot = puppet.Actions.SurroundSlot };
                if (selected.Value.ActionId != null && (selected.Value.ActionId == ActionIds.Waver || settings.GetRoleWeight(agent.Role, selected.Value.ActionId) == 0))
                {
                    PuppetStateEvent? cause = null;
                    foreach (var e in world.StateEvents)
                        if (e.AgentId.Equals(agent.Id) && (e.Kind == PuppetStateEventKind.ValueChanged || e.Kind == PuppetStateEventKind.StatusChanged)) cause = e;
                    decision.Cause = new DecisionCause(cause, agent.Role + ":" + selected.Value.ActionId,
                        context.Blackboard.RoleIntention(agent.Role, selected.Value.ActionId));
                }
                pending.Add(decision);
            }
            foreach (var decision in pending)
            {
                var agent = world.Get(decision.AgentId).Value;
                var next = Clamp(PuppetActions.MoveTowards(agent.Position, decision.Goal,
                    (decision.Selection.ActionId == ActionIds.KeepDistance ? settings.KeepDistanceSpeed : settings.MoveSpeed)
                    * world.GetMoveSpeedScale(agent.Id).Value * world.MovementScale(agent.Id) / settings.TicksPerSecond));
                var movementContext = new UtilityContext(world, agent, world.GetBlackboard(Side.Puppets).Value, tick);
                next = Clamp(puppets.Find(p => p.Id.Equals(agent.Id))!.Actions
                    .ConstrainMovement(movementContext, decision.Selection.ActionId, next));
                var direction = PuppetActions.Direction(agent.Position, next, agent.Direction);
                if (agent.Position.X == next.X && agent.Position.Z == next.Z && decision.Target.HasValue)
                {
                    var target = world.Get(decision.Target.Value);
                    if (target.Success)
                        direction = PuppetActions.Direction(next, target.Value.Position, agent.Direction);
                }
                var moved = world.UpdatePosition(agent.Id, next, direction);
                if (!moved.Success) return moved.Failure;
                var acted = world.UpdateAction(agent.Id, decision.Selection.ActionId, decision.Target);
                if (!acted.Success) return acted.Failure;
                decisions[agent.Id] = decision;
            }
            Tune(tick);
            lastTick = tick;
            participationVersion = world.ParticipationVersion;
            return AiFailure.None;
        }

        private void Reclaim()
        {
            int end = world.StateEvents.Count;
            for (; eventCursor < end; eventCursor++)
            {
                var e = world.StateEvents[eventCursor];
                if (e.Kind != PuppetStateEventKind.StatusChanged ||
                    (e.After.Status != PuppetStatus.Broken && e.After.Status != PuppetStatus.Downed)) continue;
                double next = Math.Min(profile.TuneBudgetPerSecond * profile.TuneBudgetMaxScale,
                    CurrentTuneBudgetPerSecond + profile.ReclaimBudgetPerString * (e.After.Status == PuppetStatus.Downed ? profile.HumanizedReclaimRatio : 1));
                world.RecordReclaim(e, next - CurrentTuneBudgetPerSecond, next);
                CurrentTuneBudgetPerSecond = next;
            }
        }
        private void Tune(long tick)
        {
            TuneTick = tick; TuneTarget = null; TuneAmount = 0;
            double best = double.NegativeInfinity;
            foreach (var state in world.GetPuppetStates())
            {
                if (state.Status != PuppetStatus.Active || state.Tension >= 1 || !formation.ContainsKey(state.Id)) continue;
                var agent = world.Get(state.Id).Value;
                double priority = -state.Tension;
                if (profile.Priority == TunePriority.OutOfPosition) priority = PuppetActions.Distance(agent.Position, formation[state.Id]);
                if (profile.Priority == TunePriority.Attacking && agent.CurrentAction == ActionIds.Attack) priority = 1;
                if (priority <= best) continue;
                best = priority; TuneTarget = state.Id;
            }
            if (!TuneTarget.HasValue || CurrentTuneBudgetPerSecond == 0) { TuneTarget = null; return; }
            TuneAmount = Math.Min(1 - world.GetPuppetState(TuneTarget.Value).Value.Tension, CurrentTuneBudgetPerSecond / settings.TicksPerSecond);
            world.TuneTension(TuneTarget.Value, TuneAmount, tick);
        }
        private Vec2 Clamp(Vec2 point) => new Vec2(
            Math.Max(-settings.ArenaHalfSize, Math.Min(settings.ArenaHalfSize, point.X)),
            Math.Max(-settings.ArenaHalfSize, Math.Min(settings.ArenaHalfSize, point.Z)));
    }
}
