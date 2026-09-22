namespace BehindTheCurtain.Core
{
    public sealed class FairnessRules
    {
        // Provisional design value, to be tuned after playtesting.
        public const long DefaultMinCounterWindowTicks = 10;
        // Provisional design values, to be tuned after playtesting.
        public const int DefaultMaxOpenCounterWindows = 2;
        public const int DefaultMaxRequiredPointsPerAttack = 2;

        public FairnessRules(long minCounterWindowTicks = DefaultMinCounterWindowTicks,
            int maxOpenCounterWindows = DefaultMaxOpenCounterWindows,
            int maxRequiredPointsPerAttack = DefaultMaxRequiredPointsPerAttack)
        {
            MinCounterWindowTicks = minCounterWindowTicks;
            MaxOpenCounterWindows = maxOpenCounterWindows;
            MaxRequiredPointsPerAttack = maxRequiredPointsPerAttack;
        }

        public long MinCounterWindowTicks { get; }
        public int MaxOpenCounterWindows { get; }
        public int MaxRequiredPointsPerAttack { get; }
        internal bool IsValid => MinCounterWindowTicks > 0 && MaxOpenCounterWindows > 0
            && MaxRequiredPointsPerAttack > 0;
    }
}
