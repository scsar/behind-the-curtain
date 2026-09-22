namespace BehindTheCurtain.Core
{
    /// <summary>Owns memory for one expedition. Create a new instance for the next expedition.</summary>
    public sealed class Expedition
    {
        private readonly ExpeditionSettings settings;
        private readonly ITacticSelector selector;
        private bool encounterActive;
        private bool ended;

        private Expedition(ExpeditionSettings settings, ITacticSelector selector)
        {
            this.settings = settings;
            this.selector = selector;
        }

        /// <summary>Null means no encounter has completed, or the expedition has ended.</summary>
        public BehaviorProfile? Memory { get; private set; }

        /// <summary>Rejects null dependencies and decay outside [0, 1), including NaN.</summary>
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
            encounterActive = true;
            return true;
        }

        /// <summary>Merges the completed observer profile once; invalid transitions leave memory unchanged.</summary>
        public bool EndEncounter(BehaviorProfile profile)
        {
            if (ended || !encounterActive)
                return false;
            Memory = Memory.HasValue ? Memory.Value.Merge(profile, settings.Decay) : profile;
            encounterActive = false;
            return true;
        }

        /// <summary>Clears memory, discards any active encounter, and permanently closes this expedition.</summary>
        public void EndExpedition()
        {
            Memory = null;
            encounterActive = false;
            ended = true;
        }
    }
}
