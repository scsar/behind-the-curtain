namespace BehindTheCurtain.Core
{
    /// <summary>Immutable encounter ratios in [0, 1]. The default profile is all zero.</summary>
    public readonly struct BehaviorProfile
    {
        private BehaviorProfile(double area, double healer, double sustainedRanged)
        {
            AreaDependency = area;
            HealerFocus = healer;
            SustainedRangedRatio = sustainedRanged;
        }

        // Callers supply a validated decay in [0, 1); convex averages remain in [0, 1].
        internal BehaviorProfile Merge(BehaviorProfile current, double decay) => new BehaviorProfile(
            decay * AreaDependency + (1 - decay) * current.AreaDependency,
            decay * HealerFocus + (1 - decay) * current.HealerFocus,
            decay * SustainedRangedRatio + (1 - decay) * current.SustainedRangedRatio);

        internal BehaviorProfile(long total, long area, long healer, long sustainedRanged)
        {
            AreaDependency = total == 0 ? 0 : (double)area / total;
            HealerFocus = total == 0 ? 0 : (double)healer / total;
            SustainedRangedRatio = total == 0 ? 0 : (double)sustainedRanged / total;
        }

        public double AreaDependency { get; }
        public double HealerFocus { get; }
        public double SustainedRangedRatio { get; }
    }
}
