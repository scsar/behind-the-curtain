namespace BehindTheCurtain.Core
{
    public enum CounterFailureReason
    {
        None,
        OutsideWindow,
        MethodMismatch,
        AlreadyUsed,
        ConnectionPointNotFound
    }

    public readonly struct CounterResult
    {
        internal CounterResult(CounterFailureReason failureReason)
        {
            FailureReason = failureReason;
        }

        public bool Success => FailureReason == CounterFailureReason.None;
        public CounterFailureReason FailureReason { get; }
    }

    public static class CounterResolver
    {
        /// <summary>
        /// Resolves one method against an exposed connection instance. Failure leaves the attack unchanged.
        /// Precedence: already used, outside window (including stale ticks), missing connection, wrong method.
        /// A null attack has no window. Success advances to the attempt tick and breaks the attack.
        /// Calls must be serialized by the attack's owner, as with Advance and Break.
        /// </summary>
        public static CounterResult Resolve(JointAttack? attack, long attemptTick,
            InterferenceMethod method, ConnectionPoint? target)
        {
            if (attack != null && attack.State == JointAttackState.Broken)
                return new CounterResult(CounterFailureReason.AlreadyUsed);

            if (attack == null || attack.State == JointAttackState.Executed ||
                attemptTick < attack.CurrentTick || !attack.CounterWindow.Contains(attemptTick))
                return new CounterResult(CounterFailureReason.OutsideWindow);

            var found = false;
            foreach (var point in attack.ConnectionPoints)
            {
                if (ReferenceEquals(point, target))
                {
                    found = true;
                    break;
                }
            }

            if (!found || target == null)
                return new CounterResult(CounterFailureReason.ConnectionPointNotFound);
            if (!target.Accepts(method))
                return new CounterResult(CounterFailureReason.MethodMismatch);

            attack.Advance(attemptTick);
            attack.Break();
            return new CounterResult(CounterFailureReason.None);
        }
    }
}
