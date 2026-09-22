using System;

namespace BehindTheCurtain.Prototype
{
    /// <summary>Converts accumulated frame seconds to discrete 30 Hz ticks.</summary>
    public sealed class PrototypeClock
    {
        private double accumulatedTicks;
        public long Tick { get; private set; }
        public float Interpolation => (float)accumulatedTicks;

        public void Advance(float deltaTime, Action<long> onTick)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                return;
            accumulatedTicks += (double)deltaTime * PrototypeSettings.TicksPerSecond;
            while (accumulatedTicks >= 1d)
            {
                accumulatedTicks -= 1d;
                onTick(++Tick);
            }
        }
    }
}
