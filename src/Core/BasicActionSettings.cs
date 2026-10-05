namespace BehindTheCurtain.Core
{
    /// <summary>Provisional, arbitrarily chosen T16b-3a-1 tuning. Distances are metres, angles degrees, durations ticks.</summary>
    public sealed class BasicActionSettings
    {
        public BasicActionSettings(double flankAngle = 90, double flankRadius = 3.5, double harassMargin = 1,
            int harassIntervalTicks = 90, int harassRecoverTicks = 30, double flankNonMeleeScale = .3,
            double harassMeleeScale = .3, double flankWeight = .25, double harassWeight = .2)
        {
            FlankAngle = flankAngle; FlankRadius = flankRadius; HarassMargin = harassMargin;
            HarassIntervalTicks = harassIntervalTicks; HarassRecoverTicks = harassRecoverTicks;
            FlankNonMeleeScale = flankNonMeleeScale; HarassMeleeScale = harassMeleeScale;
            FlankWeight = flankWeight; HarassWeight = harassWeight;
        }
        public double FlankAngle { get; }
        public double FlankRadius { get; }
        public double HarassMargin { get; }
        public int HarassIntervalTicks { get; }
        public int HarassRecoverTicks { get; }
        public double FlankNonMeleeScale { get; }
        public double HarassMeleeScale { get; }
        public double FlankWeight { get; }
        public double HarassWeight { get; }
    }
}
