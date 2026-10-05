using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BehindTheCurtain.Core
{
    /// <summary>Immutable, already attenuated individual memories; no party or intention state.</summary>
    public sealed class ExpeditionMemorySnapshot
    {
        internal ExpeditionMemorySnapshot(Dictionary<AgentId, BehaviorProfile> profiles)
        { IndividualMemory = new ReadOnlyDictionary<AgentId, BehaviorProfile>(profiles); }
        public IReadOnlyDictionary<AgentId, BehaviorProfile> IndividualMemory { get; }
    }
}
