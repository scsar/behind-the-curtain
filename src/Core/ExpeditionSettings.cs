namespace BehindTheCurtain.Core
{
    public sealed class ExpeditionSettings
    {
        // Provisional design value specified for expedition memory; tune after playtesting.
        public const double DefaultDecay = 0.5;

        public ExpeditionSettings(double decay = DefaultDecay, IntentionSettings? intentions = null)
        { Decay = decay; Intentions = intentions ?? new IntentionSettings(); }
        public IntentionSettings Intentions { get; }

        public double Decay { get; }
        internal bool IsValid => Decay >= 0 && Decay < 1 && Intentions.IsValid;
    }
}
