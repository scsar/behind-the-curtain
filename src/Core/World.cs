using System;
using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    public readonly struct Vec2
    {
        public Vec2(double x, double z) { X = x; Z = z; }
        public double X { get; }
        public double Z { get; }
        internal bool IsFinite => AiNumber.Finite(X) && AiNumber.Finite(Z);
    }

    public readonly struct AgentId : IEquatable<AgentId>
    {
        public AgentId(int value) { Value = value; }
        public int Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(AgentId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is AgentId other && Equals(other);
        public override int GetHashCode() => Value;
    }

    public enum Side { Puppets, Players }

    /// <summary>Immutable snapshot. Direction is a nonzero planar vector, not an angle.</summary>
    public sealed class AgentState
    {
        public AgentState(AgentId id, Side side, bool isHuman, TargetRole role, Vec2 position,
            Vec2 direction, double radius, string? currentAction = null, AgentId? target = null)
        {
            Id = id; Side = side; IsHuman = isHuman; Role = role; Position = position;
            Direction = direction; Radius = radius; CurrentAction = currentAction; Target = target;
        }
        public AgentId Id { get; }
        public Side Side { get; }
        public bool IsHuman { get; }
        public TargetRole Role { get; }
        public Vec2 Position { get; }
        public Vec2 Direction { get; }
        public double Radius { get; }
        public string? CurrentAction { get; }
        public AgentId? Target { get; }
    }

    public sealed partial class World
    {
        private readonly Dictionary<AgentId, AgentState> agents = new Dictionary<AgentId, AgentState>();
        private readonly Blackboard puppets = new Blackboard();
        private readonly Blackboard players = new Blackboard();

        /// <summary>Immutable snapshots ordered by id, independent of insertion order.</summary>
        public IReadOnlyList<AgentState> GetAgents()
        {
            var result = new List<AgentState>(agents.Values);
            result.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
            return result.AsReadOnly();
        }

        public AiResult<Blackboard> GetBlackboard(Side side) => side == Side.Puppets
            ? AiResult<Blackboard>.Ok(puppets) : side == Side.Players
            ? AiResult<Blackboard>.Ok(players) : AiResult<Blackboard>.Fail(AiFailure.InvalidSide);

        public AiResult<AgentState> Get(AgentId id)
        {
            if (!id.IsValid) return AiResult<AgentState>.Fail(AiFailure.InvalidId);
            return agents.TryGetValue(id, out var agent) ? AiResult<AgentState>.Ok(agent)
                : AiResult<AgentState>.Fail(AiFailure.AgentNotFound);
        }

        public AiResult<AgentState> Add(AgentState? agent)
            => Add(agent, new PuppetInitialState());

        public AiResult<AgentState> Add(AgentState? agent, PuppetInitialState initialState)
        {
            if (agent == null || !agent.Id.IsValid) return AiResult<AgentState>.Fail(AiFailure.InvalidId);
            if (agents.ContainsKey(agent.Id)) return AiResult<AgentState>.Fail(AiFailure.DuplicateAgent);
            var failure = Validate(agent);
            if (failure != AiFailure.None) return AiResult<AgentState>.Fail(failure);
            if (agent.Side == Side.Puppets && (initialState == null || !initialState.IsValid || StateSettings == null || !StateSettings.IsValid
                || (initialState.Values.TryGetValue(PuppetValueId.Posture, out var posture) && posture > StateSettings.MaxPosture)))
                return AiResult<AgentState>.Fail(AiFailure.InvalidValue);
            agents.Add(agent.Id, agent);
            if (agent.Side == Side.Puppets) AddPuppetState(agent.Id, initialState);
            return AiResult<AgentState>.Ok(agent);
        }

        public AiResult<AgentState> UpdatePosition(AgentId id, Vec2 position, Vec2 direction)
        {
            var found = Get(id);
            if (!found.Success) return found;
            var a = found.Value;
            return Replace(new AgentState(id, a.Side, a.IsHuman, a.Role, position, direction,
                a.Radius, a.CurrentAction, a.Target));
        }

        public AiResult<AgentState> UpdateAction(AgentId id, string? action, AgentId? target = null)
        {
            var found = Get(id);
            if (!found.Success) return found;
            var a = found.Value;
            return Replace(new AgentState(id, a.Side, a.IsHuman, a.Role, a.Position, a.Direction,
                a.Radius, action, target));
        }

        private AiResult<AgentState> Replace(AgentState agent)
        {
            var failure = Validate(agent);
            if (failure != AiFailure.None) return AiResult<AgentState>.Fail(failure);
            agents[agent.Id] = agent;
            return AiResult<AgentState>.Ok(agent);
        }

        private AiFailure Validate(AgentState a)
        {
            if (a.Side != Side.Puppets && a.Side != Side.Players) return AiFailure.InvalidSide;
            if (a.Role != TargetRole.Guard && a.Role != TargetRole.Healer && a.Role != TargetRole.Attacker)
                return AiFailure.InvalidRole;
            if (!a.Position.IsFinite) return AiFailure.InvalidPosition;
            if (!a.Direction.IsFinite || (a.Direction.X == 0 && a.Direction.Z == 0)) return AiFailure.InvalidDirection;
            if (!AiNumber.Weight(a.Radius)) return AiFailure.InvalidRadius;
            if (a.CurrentAction != null && string.IsNullOrWhiteSpace(a.CurrentAction)) return AiFailure.InvalidAction;
            if (a.Target.HasValue && (!a.Target.Value.IsValid || !agents.ContainsKey(a.Target.Value)))
                return AiFailure.TargetNotFound;
            if (a.Target.HasValue && a.CurrentAction == null) return AiFailure.InvalidAction;
            return AiFailure.None;
        }
    }
}
