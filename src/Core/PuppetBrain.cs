using System;
using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    public sealed class PuppetDecision
    {
        internal PuppetDecision(AgentId id, UtilitySelection selection, Vec2 goal, AgentId? target)
        { AgentId = id; Selection = selection; Goal = goal; Target = target; }
        public AgentId AgentId { get; }
        public UtilitySelection Selection { get; }
        /// <summary>Arena-clamped destination used for this tick, including stationary attacks.</summary>
        public Vec2 Goal { get; }
        public AgentId? Target { get; }
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
        private long? lastTick;
        private long firstTick;
        private PuppetBrain(World world, PuppetSettings settings, List<Puppet> puppets)
        { this.world = world; this.settings = settings; this.puppets = puppets; }

        public static AiResult<PuppetBrain> Create(World world, PuppetSettings settings, DeterministicRandom random)
        {
            if (world == null) return AiResult<PuppetBrain>.Fail(AiFailure.InvalidValue);
            if (settings == null || !settings.IsValid || random == null)
                return AiResult<PuppetBrain>.Fail(AiFailure.InvalidSettings);
            var puppets = new List<Puppet>();
            ulong encounterSeed = (ulong)(random.NextDouble() * uint.MaxValue);
            foreach (var agent in world.GetAgents())
            {
                if (agent.Side != Side.Puppets) continue;
                var actions = new PuppetActions(settings, agent.Position);
                var selector = UtilitySelector.Create(world, agent.Id, actions.Create(agent.Role, new DeterministicRandom(encounterSeed ^ (ulong)agent.Id.Value)),
                    new UtilitySettings(1, settings.PersistenceBonus), random);
                if (!selector.Success) return AiResult<PuppetBrain>.Fail(selector.Failure);
                puppets.Add(new Puppet(agent.Id, actions, selector.Value));
            }
            return AiResult<PuppetBrain>.Ok(new PuppetBrain(world, settings, puppets));
        }

        /// <summary>No decision exists before the first successful tick.</summary>
        public AiResult<PuppetDecision> GetDecision(AgentId id) => decisions.TryGetValue(id, out var value)
            ? AiResult<PuppetDecision>.Ok(value) : AiResult<PuppetDecision>.Fail(AiFailure.AgentNotFound);

        public AiFailure Tick(long tick)
        {
            if (lastTick.HasValue && (lastTick.Value == long.MaxValue || tick != lastTick.Value + 1))
                return AiFailure.InvalidTick;
            if (!lastTick.HasValue) firstTick = tick;
            var pending = new List<PuppetDecision>();
            int ordinal = 0;
            foreach (var puppet in puppets)
            {
                int offset = (int)((long)ordinal++ * settings.ReevaluationIntervalTicks / puppets.Count);
                bool evaluate = !decisions.ContainsKey(puppet.Id) ||
                    unchecked((ulong)tick - (ulong)firstTick) % (ulong)settings.ReevaluationIntervalTicks == (ulong)offset;
                var selected = evaluate ? puppet.Selector.Select(tick)
                    : AiResult<UtilitySelection>.Ok(new UtilitySelection(decisions[puppet.Id].Selection.ActionId,
                        decisions[puppet.Id].Selection.EvaluatedAtTick, false, decisions[puppet.Id].Selection.Scores));
                if (!selected.Success) return selected.Failure;
                var agent = world.Get(puppet.Id).Value;
                var context = new UtilityContext(world, agent, world.GetBlackboard(Side.Puppets).Value, tick);
                puppet.Actions.Reserve(context, selected.Value.ActionId);
                var resolved = puppet.Actions.Resolve(context, selected.Value.ActionId);
                pending.Add(new PuppetDecision(puppet.Id, selected.Value, Clamp(resolved.Goal), resolved.Target));
            }
            foreach (var decision in pending)
            {
                var agent = world.Get(decision.AgentId).Value;
                var next = Clamp(PuppetActions.MoveTowards(agent.Position, decision.Goal,
                    (decision.Selection.ActionId == ActionIds.KeepDistance ? settings.KeepDistanceSpeed : settings.MoveSpeed) / settings.TicksPerSecond));
                var movementContext = new UtilityContext(world, agent, world.GetBlackboard(Side.Puppets).Value, tick);
                next = Clamp(puppets.Find(p => p.Id.Equals(agent.Id))!.Actions
                    .ConstrainMovement(movementContext, decision.Selection.ActionId, next));
                var moved = world.UpdatePosition(agent.Id, next,
                    PuppetActions.Direction(agent.Position, next, agent.Direction));
                if (!moved.Success) return moved.Failure;
                var acted = world.UpdateAction(agent.Id, decision.Selection.ActionId, decision.Target);
                if (!acted.Success) return acted.Failure;
                decisions[agent.Id] = decision;
            }
            lastTick = tick;
            return AiFailure.None;
        }

        private Vec2 Clamp(Vec2 point) => new Vec2(
            Math.Max(-settings.ArenaHalfSize, Math.Min(settings.ArenaHalfSize, point.X)),
            Math.Max(-settings.ArenaHalfSize, Math.Min(settings.ArenaHalfSize, point.Z)));
    }
}
