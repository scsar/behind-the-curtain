using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BehindTheCurtain.Core
{
    /// <summary>Owns memory for one expedition. Create a new instance for the next expedition.</summary>
    public sealed class Expedition
    {
        private readonly ExpeditionSettings settings;
        private readonly ITacticSelector selector;
        private readonly Dictionary<AgentId, BehaviorProfile> individualMemory = new Dictionary<AgentId, BehaviorProfile>();
        public IReadOnlyDictionary<AgentId, BehaviorProfile> IndividualMemory =>
            new ReadOnlyDictionary<AgentId, BehaviorProfile>(individualMemory);
        /// <summary>A fresh world per encounter. Director decisions are written only on successful start.</summary>
        public World? EncounterWorld { get; private set; }
        private bool encounterActive;
        private bool ended;

        private Expedition(ExpeditionSettings settings, ITacticSelector selector)
        {
            this.settings = settings;
            this.selector = selector;
        }

        /// <summary>Null means no encounter has completed, or the expedition has ended.</summary>
        public BehaviorProfile? Memory { get; private set; }

        /// <summary>Rejects null dependencies and invalid memory weights or intentions.</summary>
        public static bool TryCreate(ExpeditionSettings? settings, ITacticSelector? selector,
            out Expedition? expedition)
        {
            expedition = null;
            if (settings == null || !settings.IsValid || selector == null)
                return false;
            expedition = new Expedition(settings, selector);
            return true;
        }

        /// <summary>The first encounter uses StandardFormation with no additional preparation ticks.</summary>
        public bool TryStartEncounter(out Tactic tactic)
        {
            tactic = default;
            if (ended || encounterActive)
                return false;
            tactic = Memory.HasValue ? selector.Select(Memory.Value)
                : new Tactic(TacticId.StandardFormation, TacticWeakness.None, 0);
            EncounterWorld = new World();
            var board = EncounterWorld.GetBlackboard(Side.Puppets).Value;
            foreach (var pair in settings.Intentions.Tables[tactic.Id]) board.SetIntention(pair.Key, pair.Value);
            board.SetFact("tactic", tactic.Id);
            SetThreat(board, TargetFactIds.HealerThreat, TendencyId.HealerFocus);
            SetThreat(board, TargetFactIds.RangedThreat, TendencyId.SustainedRanged);
            encounterActive = true;
            return true;
        }

        /// <summary>Merges the completed observer profile once; invalid transitions leave memory unchanged.</summary>
        public bool EndEncounter(BehaviorProfile profile)
        {
            return EndEncounterWithIndividuals(new EncounterObservation(profile, EncounterObservation.Empty));
        }

        public bool EndEncounterWithIndividuals(EncounterObservation observation)
        {
            if (ended || !encounterActive) return false;
            var profile = observation.PartyProfile;
            Memory = Memory.HasValue ? Memory.Value.Merge(profile, settings) : profile;
            // Missing attackers lose suspicion more slowly than observed changes in behavior.
            foreach (var id in new List<AgentId>(individualMemory.Keys))
            {
                observation.IndividualProfiles.TryGetValue(id, out var current);
                individualMemory[id] = individualMemory[id].Merge(current, settings);
            }
            foreach (var pair in observation.IndividualProfiles)
                if (!individualMemory.ContainsKey(pair.Key)) individualMemory.Add(pair.Key, pair.Value);
            encounterActive = false;
            return true;
        }

        private void SetThreat(Blackboard board, string key, TendencyId tendency)
        {
            AgentId? best = null;
            double highest = 0;
            foreach (var pair in individualMemory)
            {
                var value = pair.Value[tendency];
                if (value > highest || (value > 0 && value == highest && best.HasValue && pair.Key.Value < best.Value.Value))
                { best = pair.Key; highest = value; }
            }
            if (best.HasValue && highest >= settings.ThreatFactThreshold) board.SetFact(key, best.Value);
        }

        /// <summary>Clears memory, discards any active encounter, and permanently closes this expedition.</summary>
        public void EndExpedition()
        {
            Memory = null;
            individualMemory.Clear();
            EncounterWorld = null;
            encounterActive = false;
            ended = true;
        }
    }
}
