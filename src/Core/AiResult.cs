namespace BehindTheCurtain.Core
{
    public enum AiFailure
    {
        None, InvalidId, InvalidSide, InvalidRole, InvalidPosition, InvalidDirection,
        InvalidRadius, DuplicateAgent, AgentNotFound, InvalidAction, TargetNotFound,
        InvalidKey, InvalidValue, FactNotFound, InvalidTick, ReservationConflict,
        ReservationNotFound, InvalidRange, InvalidSettings, InvalidConsideration,
        InvalidInput, InvalidCurve, InvalidWeight, DuplicateAction
    }

    public readonly struct AiResult<T>
    {
        private AiResult(T value, AiFailure failure) { Value = value; Failure = failure; }
        public T Value { get; }
        public AiFailure Failure { get; }
        public bool Success => Failure == AiFailure.None;
        public static AiResult<T> Ok(T value) => new AiResult<T>(value, AiFailure.None);
        public static AiResult<T> Fail(AiFailure failure) => new AiResult<T>(default!, failure);
    }

    internal static class AiNumber
    {
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        internal static bool Weight(double value) => Finite(value) && value >= 0;
        internal static double Clamp(double value) => System.Math.Max(0, System.Math.Min(1, value));
    }
}
