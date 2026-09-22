namespace BehindTheCurtain.Core
{
    public sealed class FairnessRules
    {
        // Provisional design value, to be tuned after playtesting.
        public const long DefaultMinCounterWindowTicks = 10;

        public FairnessRules(long minCounterWindowTicks = DefaultMinCounterWindowTicks)
            => MinCounterWindowTicks = minCounterWindowTicks;

        public long MinCounterWindowTicks { get; }
    }
}
