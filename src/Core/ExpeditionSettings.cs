namespace BehindTheCurtain.Core
{
    public sealed class ExpeditionSettings
    {
        // Provisional design value specified for expedition memory; tune after playtesting.
        public const double DefaultDecay = 0.5;

        public ExpeditionSettings(double decay = DefaultDecay) => Decay = decay;

        public double Decay { get; }
        internal bool IsValid => Decay >= 0 && Decay < 1;
    }
}
