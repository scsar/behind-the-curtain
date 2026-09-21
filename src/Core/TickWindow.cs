namespace BehindTheCurtain.Core
{
    /// <summary>
    /// An inclusive interval of signed integer ticks. The default value is [0, 0].
    /// </summary>
    public readonly struct TickWindow
    {
        private TickWindow(long start, long end)
        {
            Start = start;
            End = end;
        }

        public long Start { get; }
        public long End { get; }

        /// <summary>
        /// Returns false and a default window when start is later than end.
        /// Negative ticks are allowed.
        /// </summary>
        public static bool TryCreate(long start, long end, out TickWindow window)
        {
            if (start > end)
            {
                window = default;
                return false;
            }

            window = new TickWindow(start, end);
            return true;
        }

        public bool Contains(long tick) => Start <= tick && tick <= End;

        /// <summary>Returns true when the windows share at least one tick.</summary>
        public bool Overlaps(TickWindow other) => Start <= other.End && other.Start <= End;
    }
}
