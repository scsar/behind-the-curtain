using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    /// <summary>Immutable party and human-attacker snapshots produced by CombatObserver.</summary>
    public readonly struct EncounterObservation
    {
        private readonly IReadOnlyDictionary<AgentId, BehaviorProfile>? individuals;
        internal EncounterObservation(BehaviorProfile partyProfile,
            IReadOnlyDictionary<AgentId, BehaviorProfile> individualProfiles)
        { PartyProfile = partyProfile; individuals = individualProfiles; }
        public BehaviorProfile PartyProfile { get; }
        public IReadOnlyDictionary<AgentId, BehaviorProfile> IndividualProfiles => individuals ?? Empty;
        internal static readonly IReadOnlyDictionary<AgentId, BehaviorProfile> Empty =
            new System.Collections.ObjectModel.ReadOnlyDictionary<AgentId, BehaviorProfile>(
                new Dictionary<AgentId, BehaviorProfile>());
    }
}
