using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    public sealed partial class World
    {
        private readonly Dictionary<AgentId, Temperament> temperaments = new Dictionary<AgentId, Temperament>();
        public AiResult<Temperament> GetTemperament(AgentId id)
        {
            var found = Get(id);
            if (!found.Success) return AiResult<Temperament>.Fail(found.Failure);
            if (found.Value.Side != Side.Puppets) return AiResult<Temperament>.Fail(AiFailure.InvalidSide);
            return AiResult<Temperament>.Ok(temperaments.TryGetValue(id, out var value) ? value : Temperament.Zero);
        }
        public AiResult<Temperament> SetTemperament(AgentId id, Temperament temperament)
        {
            var found = GetTemperament(id);
            if (!found.Success) return found;
            if (temperament == null || !temperament.IsValid) return AiResult<Temperament>.Fail(AiFailure.InvalidValue);
            temperaments[id] = temperament;
            return AiResult<Temperament>.Ok(temperament);
        }
    }
}
