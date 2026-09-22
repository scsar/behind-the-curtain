namespace BehindTheCurtain.Core
{
    public sealed class ObserverSettings
    {
        // Provisional design values; tune after playtesting.
        public const int DefaultMinimumAreaHitCount = 2;
        public const int DefaultSustainedRangedStreak = 3;

        public ObserverSettings(int minimumAreaHitCount = DefaultMinimumAreaHitCount,
            int sustainedRangedStreak = DefaultSustainedRangedStreak)
        {
            MinimumAreaHitCount = minimumAreaHitCount;
            SustainedRangedStreak = sustainedRangedStreak;
        }

        public int MinimumAreaHitCount { get; }
        public int SustainedRangedStreak { get; }
        internal bool IsValid => MinimumAreaHitCount > 0 && SustainedRangedStreak > 0;
    }
}
