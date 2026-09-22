namespace BehindTheCurtain.Core
{
    public sealed class ExpeditionSettings
    {
        // Provisional design value specified for expedition memory; tune after playtesting.
        public const double DefaultDecay = 0.5;

        public const double DefaultFallWeight = 0.2;
        public const double DefaultAbsentFallWeight = 0.1;
        private readonly bool validLegacyDecay;

        /// <summary>Explicit riseWeight overrides decay. Otherwise the legacy [0,1) decay maps to 1-decay.</summary>
        public ExpeditionSettings(double decay = DefaultDecay, IntentionSettings? intentions = null,
            double? riseWeight = null, double fallWeight = DefaultFallWeight,
            double absentFallWeight = DefaultAbsentFallWeight)
        {
            RiseWeight = riseWeight ?? 1 - decay;
            validLegacyDecay = riseWeight.HasValue || (decay >= 0 && decay < 1);
            Decay = riseWeight.HasValue ? 1 - riseWeight.Value : decay;
            FallWeight = fallWeight; AbsentFallWeight = absentFallWeight;
            Intentions = intentions ?? new IntentionSettings();
        }
        public IntentionSettings Intentions { get; }

        public double Decay { get; }
        public double RiseWeight { get; }
        public double FallWeight { get; }
        public double AbsentFallWeight { get; }
        internal bool IsValid => validLegacyDecay && RiseWeight >= 0 && RiseWeight <= 1
            && FallWeight >= 0 && FallWeight <= 1
            && AbsentFallWeight >= 0 && AbsentFallWeight <= 1 && Intentions.IsValid;
    }
}
