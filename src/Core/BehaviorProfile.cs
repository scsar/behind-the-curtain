namespace BehindTheCurtain.Core
{
    /// <summary>Immutable encounter ratios in [0, 1]. The default profile is all zero.</summary>
    public readonly struct BehaviorProfile
    {
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
