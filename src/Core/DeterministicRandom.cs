namespace BehindTheCurtain.Core
{
    /// <summary>SplitMix64. Integer arithmetic and the upper 53 bits define the sequence.</summary>
    public sealed class DeterministicRandom
    {
        private ulong state;
        public DeterministicRandom(ulong seed) { state = seed; }

        private ulong NextUInt64()
        {
            unchecked
            {
                ulong z = (state += 0x9E3779B97F4A7C15UL);
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Returns a value in [0, 1).</summary>
        public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

        /// <summary>Unbiased [minInclusive, maxExclusive). Invalid ranges do not consume randomness.</summary>
        public AiResult<int> NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive) return AiResult<int>.Fail(AiFailure.InvalidRange);
            ulong width = (ulong)((long)maxExclusive - minInclusive);
            ulong threshold = unchecked(0UL - width) % width;
            ulong value;
            do { value = NextUInt64(); } while (value < threshold);
            return AiResult<int>.Ok((int)(minInclusive + (long)(value % width)));
        }
    }
}
