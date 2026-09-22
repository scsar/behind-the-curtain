namespace BehindTheCurtain.Core
{
    public sealed class TacticSettings
    {
        // All thresholds and costs are provisional design values; tune after playtesting.
        public const double DefaultThreshold = 0.5;
        public const long DefaultSpreadFormationPreparationTickCost = 10;
        public const long DefaultGuardHealerPreparationTickCost = 5;
        public const long DefaultShieldAdvancePreparationTickCost = 15;
        public const long DefaultStandardFormationPreparationTickCost = 0;

        public TacticSettings(double areaDependencyThreshold = DefaultThreshold,
            double healerFocusThreshold = DefaultThreshold,
            double sustainedRangedThreshold = DefaultThreshold,
            long spreadFormationPreparationTickCost = DefaultSpreadFormationPreparationTickCost,
            long guardHealerPreparationTickCost = DefaultGuardHealerPreparationTickCost,
            long shieldAdvancePreparationTickCost = DefaultShieldAdvancePreparationTickCost,
            long standardFormationPreparationTickCost = DefaultStandardFormationPreparationTickCost)
        {
            AreaDependencyThreshold = areaDependencyThreshold;
            HealerFocusThreshold = healerFocusThreshold;
            SustainedRangedThreshold = sustainedRangedThreshold;
            SpreadFormationPreparationTickCost = spreadFormationPreparationTickCost;
            GuardHealerPreparationTickCost = guardHealerPreparationTickCost;
            ShieldAdvancePreparationTickCost = shieldAdvancePreparationTickCost;
            StandardFormationPreparationTickCost = standardFormationPreparationTickCost;
        }

        public double AreaDependencyThreshold { get; }
        public double HealerFocusThreshold { get; }
        public double SustainedRangedThreshold { get; }
        public long SpreadFormationPreparationTickCost { get; }
        public long GuardHealerPreparationTickCost { get; }
        public long ShieldAdvancePreparationTickCost { get; }
        public long StandardFormationPreparationTickCost { get; }

        internal bool IsValid => IsThreshold(AreaDependencyThreshold)
            && IsThreshold(HealerFocusThreshold) && IsThreshold(SustainedRangedThreshold)
            && SpreadFormationPreparationTickCost >= 0 && GuardHealerPreparationTickCost >= 0
            && ShieldAdvancePreparationTickCost >= 0 && StandardFormationPreparationTickCost >= 0;

        private static bool IsThreshold(double value) => value >= 0 && value <= 1;
    }
}
