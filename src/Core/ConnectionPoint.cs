using System;

namespace BehindTheCurtain.Core
{
    [Flags]
    public enum InterferenceMethod
    {
        Cut = 1,
        Shoot = 2,
        Disrupt = 4
    }

    /// <summary>An exposed connection, identified by its object instance.</summary>
    public sealed class ConnectionPoint
    {
        public ConnectionPoint(InterferenceMethod allowedMethods)
        {
            AllowedMethods = allowedMethods;
        }

        public InterferenceMethod AllowedMethods { get; }

        internal bool IsValid => AllowedMethods != 0 &&
            (AllowedMethods & ~(InterferenceMethod.Cut | InterferenceMethod.Shoot | InterferenceMethod.Disrupt)) == 0;

        internal bool Accepts(InterferenceMethod method) =>
            (method == InterferenceMethod.Cut || method == InterferenceMethod.Shoot ||
             method == InterferenceMethod.Disrupt) && (AllowedMethods & method) != 0;
    }
}
