namespace BehindTheCurtain.Core
{
    /// <summary>Provisional, arbitrarily chosen T16b-3a surround weights.</summary>
    public sealed class SurroundSettings
    {
        public SurroundSettings(double attackerWeight = .35, double guardWeight = .2)
        { AttackerWeight = attackerWeight; GuardWeight = guardWeight; }
        public double AttackerWeight { get; }
        public double GuardWeight { get; }
    }
    /// <summary>Provisional, arbitrarily chosen T16b-3a-1 tuning. Distances are metres, angles degrees, durations ticks.</summary>
    public sealed class BasicActionSettings
    {
        public BasicActionSettings(double flankAngle = 90, double flankRadius = 3.5, double harassMargin = 1,
            int harassIntervalTicks = 90, int harassRecoverTicks = 30, double flankNonMeleeScale = .3,
            double harassMeleeScale = .3, double flankWeight = .3, double harassWeight = .3,
            double approachNonMeleeScale = .5, double keepAlignTolerance = 20)
            : this(new SurroundSettings(), flankAngle, flankRadius, harassMargin, harassIntervalTicks, harassRecoverTicks, flankNonMeleeScale, harassMeleeScale, flankWeight, harassWeight, approachNonMeleeScale, keepAlignTolerance) { }

        /// <summary>Provisional, arbitrarily chosen surround weights.</summary>
        public BasicActionSettings(SurroundSettings surround, double flankAngle = 90, double flankRadius = 3.5, double harassMargin = 1,
            int harassIntervalTicks = 90, int harassRecoverTicks = 30, double flankNonMeleeScale = .3,
            double harassMeleeScale = .3, double flankWeight = .3, double harassWeight = .3,
            double approachNonMeleeScale = .5, double keepAlignTolerance = 20)
        {
            SurroundWeight = surround?.AttackerWeight ?? double.NaN; SurroundGuardWeight = surround?.GuardWeight ?? double.NaN;
            FlankAngle = flankAngle; FlankRadius = flankRadius; HarassMargin = harassMargin;
            HarassIntervalTicks = harassIntervalTicks; HarassRecoverTicks = harassRecoverTicks;
            FlankNonMeleeScale = flankNonMeleeScale; HarassMeleeScale = harassMeleeScale;
            FlankWeight = flankWeight; HarassWeight = harassWeight;
            ApproachNonMeleeScale = approachNonMeleeScale; KeepAlignTolerance = keepAlignTolerance;
        }
        public double SurroundWeight { get; }
        public double SurroundGuardWeight { get; }
        public double FlankAngle { get; }
        public double FlankRadius { get; }
        public double HarassMargin { get; }
        public int HarassIntervalTicks { get; }
        public int HarassRecoverTicks { get; }
        public double FlankNonMeleeScale { get; }
        public double HarassMeleeScale { get; }
        public double ApproachNonMeleeScale { get; }
        public double KeepAlignTolerance { get; }
        public double FlankWeight { get; }
        public double HarassWeight { get; }
    }
}
