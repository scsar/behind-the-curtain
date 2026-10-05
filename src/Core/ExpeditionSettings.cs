namespace BehindTheCurtain.Core
{
    public sealed class ExpeditionSettings
    {
        // Provisional design value specified for expedition memory; tune after playtesting.
        public const double DefaultDecay = 0.5;

        public const double DefaultFallWeight = 0.2;
        public const double DefaultAbsentFallWeight = 0.1;
        // Provisional learned-threat publication cutoff.
        public const double DefaultThreatFactThreshold = 0.35;
        private readonly bool validLegacyDecay;

        /// <summary>Explicit riseWeight overrides decay. Otherwise the legacy [0,1) decay maps to 1-decay.</summary>
        public ExpeditionSettings(double decay = DefaultDecay, IntentionSettings? intentions = null,
            double? riseWeight = null, double fallWeight = DefaultFallWeight,
            double absentFallWeight = DefaultAbsentFallWeight, double threatFactThreshold = DefaultThreatFactThreshold,
            double maxIntentShiftPerEncounter = 0.75, double carryOverRatio = 0.5)
        {
            MaxIntentShiftPerEncounter = maxIntentShiftPerEncounter; CarryOverRatio = carryOverRatio;
            ThreatFactThreshold = threatFactThreshold;
            RiseWeight = riseWeight ?? 1 - decay;
            validLegacyDecay = riseWeight.HasValue || (decay >= 0 && decay < 1);
            Decay = riseWeight.HasValue ? 1 - riseWeight.Value : decay;
            FallWeight = fallWeight; AbsentFallWeight = absentFallWeight;
            Intentions = intentions ?? new IntentionSettings();
        }
        public IntentionSettings Intentions { get; }

        // Provisional, arbitrarily chosen shift limit and carry-over ratio.
        public double MaxIntentShiftPerEncounter { get; }
        public double CarryOverRatio { get; }
        public double ThreatFactThreshold { get; }
        public double Decay { get; }
        public double RiseWeight { get; }
        public double FallWeight { get; }
        public double AbsentFallWeight { get; }
        internal bool IsValid => AiNumber.Weight(MaxIntentShiftPerEncounter) && CarryOverRatio >= 0 && CarryOverRatio <= 1 && ThreatFactThreshold >= 0 && ThreatFactThreshold <= 1 && validLegacyDecay && RiseWeight >= 0 && RiseWeight <= 1
            && FallWeight >= 0 && FallWeight <= 1
            && AbsentFallWeight >= 0 && AbsentFallWeight <= 1 && Intentions.IsValid;
    }
}
