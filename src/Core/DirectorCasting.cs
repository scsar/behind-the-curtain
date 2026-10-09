using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BehindTheCurtain.Core
{
    public enum ContractActionKind { Outside, Primary, Alternative }

    public sealed class RoleContract
    {
        private RoleContract(string goal, List<string> actions)
        { Goal = goal; Actions = actions.AsReadOnly(); }
        public string Goal { get; }
        public IReadOnlyList<string> Actions { get; }
        public static bool IsBasicAction(string action) => action == ActionIds.Approach || action == ActionIds.Attack
            || action == ActionIds.Surround || action == ActionIds.Flank || action == ActionIds.Harass || action == ActionIds.Pressure || action == ActionIds.KeepDistance || action == ActionIds.Hold || action == ActionIds.Spread;
        public static AiResult<RoleContract> Create(string goal, IEnumerable<string>? actions)
        {
            if (string.IsNullOrWhiteSpace(goal) || actions == null) return AiResult<RoleContract>.Fail(AiFailure.InvalidInput);
            var copy = new List<string>();
            foreach (var action in actions)
            {
                if ((!IsBasicAction(action) && action != ActionIds.Intercept) || copy.Contains(action))
                    return AiResult<RoleContract>.Fail(AiFailure.InvalidAction);
                copy.Add(action);
            }
            return copy.Count == 0 || !IsBasicAction(copy[copy.Count - 1])
                ? AiResult<RoleContract>.Fail(AiFailure.InvalidAction) : AiResult<RoleContract>.Ok(new RoleContract(goal, copy));
        }
        private static readonly RoleContract guard = Create("보호 대상의 안전", new[] { ActionIds.Intercept, ActionIds.Pressure, ActionIds.Approach, ActionIds.Surround }).Value;
        private static readonly RoleContract attacker = Create("위협자 압박", new[] { ActionIds.Approach, ActionIds.Attack, ActionIds.Flank, ActionIds.Harass, ActionIds.Surround }).Value;
        private static readonly RoleContract healer = Create("아군 생존 유지", new[] { ActionIds.KeepDistance }).Value;
        public static RoleContract ForRole(TargetRole role) => role == TargetRole.Guard ? guard : role == TargetRole.Healer ? healer : attacker;
        /// <summary>Zero-based index; -1 means outside the contract.</summary>
        public int IndexOf(string? action)
        { for (int i = 0; i < Actions.Count; i++) if (Actions[i] == action) return i; return -1; }
        public ContractActionKind Classify(string? action)
        {
            int index = IndexOf(action);
            // Attacker's approach and attack are both primary actions, without a fallback chain.
            return index < 0 ? ContractActionKind.Outside : this == attacker || index == 0 ? ContractActionKind.Primary : ContractActionKind.Alternative;
        }
        internal double EffectiveWeight(PuppetSettings settings, Loadout loadout, string action)
        {
            double weight = settings.GetRoleWeight(this == guard ? TargetRole.Guard : this == healer ? TargetRole.Healer : TargetRole.Attacker, action);
            for (int i = 0; i < Actions.Count - 1; i++)
            {
                if (Actions[i] != ActionIds.Intercept || loadout.HasCapability(CapabilityTag.Protect)) continue;
                if (action == Actions[i]) weight = 0;
                if (action == Actions[i + 1]) weight += settings.GetRoleWeight(TargetRole.Guard, Actions[i]);
            }
            return weight;
        }
    }

    public sealed class DirectorCastingSettings
    {
        // Provisional, arbitrarily chosen casting count.
        public DirectorCastingSettings(int guardCount = 2) { GuardCount = guardCount; }
        public int GuardCount { get; }
    }

    public static class DirectorCasting
    {
        /// <summary>Call after populating the encounter and before PuppetBrain.Create.</summary>
        public static AiResult<IReadOnlyDictionary<AgentId, TargetRole>> Assign(World world, DirectorCastingSettings? settings = null)
        {
            settings = settings ?? new DirectorCastingSettings();
            if (world == null) return AiResult<IReadOnlyDictionary<AgentId, TargetRole>>.Fail(AiFailure.InvalidValue);
            if (settings.GuardCount < 0) return AiResult<IReadOnlyDictionary<AgentId, TargetRole>>.Fail(AiFailure.InvalidSettings);
            var agents = world.GetAgents();
            AgentId? healer = null;
            foreach (var agent in agents)
                if (agent.Side == Side.Puppets && world.GetLoadout(agent.Id).Value.HasCapability(CapabilityTag.Heal))
                { healer = agent.Id; break; }
            var result = new Dictionary<AgentId, TargetRole>();
            int guards = 0;
            foreach (var agent in agents)
            {
                if (agent.Side != Side.Puppets) continue;
                var role = healer.HasValue && healer.Value.Equals(agent.Id) ? TargetRole.Healer
                    : guards < settings.GuardCount && world.GetLoadout(agent.Id).Value.HasCapability(CapabilityTag.Protect)
                        ? TargetRole.Guard : TargetRole.Attacker;
                if (role == TargetRole.Guard) guards++;
                world.ReplaceCastingRole(agent, role);
                result.Add(agent.Id, role);
            }
            return AiResult<IReadOnlyDictionary<AgentId, TargetRole>>.Ok(new ReadOnlyDictionary<AgentId, TargetRole>(result));
        }
    }

    public sealed partial class World
    {
        internal void ReplaceCastingRole(AgentState agent, TargetRole role) => Replace(new AgentState(agent.Id,
            agent.Side, agent.IsHuman, role, agent.Position, agent.Direction, agent.Radius, agent.CurrentAction, agent.Target));
    }
}
