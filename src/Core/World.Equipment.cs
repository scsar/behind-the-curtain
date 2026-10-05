using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    public sealed partial class World
    {
        private readonly Dictionary<AgentId, Loadout> loadouts = new Dictionary<AgentId, Loadout>();
        public AiResult<AgentState> AddWithLoadout(AgentState? agent, Loadout loadout) => Add(agent, new PuppetInitialState(), loadout);
        public AiResult<AgentState> Add(AgentState? agent, PuppetInitialState initialState, Loadout loadout)
        {
            if (loadout == null) return AiResult<AgentState>.Fail(AiFailure.InvalidValue);
            var result = Add(agent, initialState);
            if (result.Success) loadouts[result.Value.Id] = loadout;
            return result;
        }
        public AiResult<Loadout> GetLoadout(AgentId id)
        {
            var agent = Get(id);
            if (!agent.Success) return AiResult<Loadout>.Fail(agent.Failure);
            return loadouts.TryGetValue(id, out var loadout) ? AiResult<Loadout>.Ok(loadout) : AiResult<Loadout>.Fail(AiFailure.InvalidState);
        }
        public AiResult<double> GetAttackRange(AgentId id, PuppetSettings settings)
        {
            if (settings == null || !settings.IsValid) return AiResult<double>.Fail(AiFailure.InvalidSettings);
            var found = GetLoadout(id);
            return found.Success ? AiResult<double>.Ok(found.Value.GetAttackRange(settings)) : AiResult<double>.Fail(found.Failure);
        }
        public AiResult<double> GetMoveSpeedScale(AgentId id)
        {
            var found = GetLoadout(id);
            return found.Success ? AiResult<double>.Ok(found.Value.MoveSpeedScale) : AiResult<double>.Fail(found.Failure);
        }
    }
}
