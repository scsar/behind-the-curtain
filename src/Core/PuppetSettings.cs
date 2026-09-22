namespace BehindTheCurtain.Core
{
    /// <summary>Provisional, arbitrarily chosen distances, speeds and utility tuning for T15.</summary>
    public sealed class PuppetSettings
    {
        public PuppetSettings(double interceptDistance = 1.5, double spreadDistance = 3,
            double attackRange = 2, double moveSpeed = 3, double arenaHalfSize = 10,
            int ticksPerSecond = 30, double distanceScale = 10, double approachWeight = 0.2,
            double interceptWeight = 1.5, double spreadWeight = 0.15, double holdWeight = 0.1,
            double attackWeight = 0.8, double interceptPositionFloor = 0.6,
            double persistenceBonus = 0.02)
        {
            InterceptDistance = interceptDistance; SpreadDistance = spreadDistance;
            AttackRange = attackRange; MoveSpeed = moveSpeed; ArenaHalfSize = arenaHalfSize;
            TicksPerSecond = ticksPerSecond; DistanceScale = distanceScale;
            ApproachWeight = approachWeight; InterceptWeight = interceptWeight;
            SpreadWeight = spreadWeight; HoldWeight = holdWeight; AttackWeight = attackWeight;
            InterceptPositionFloor = interceptPositionFloor; PersistenceBonus = persistenceBonus;
        }
        public double InterceptDistance { get; }
        public double SpreadDistance { get; }
        public double AttackRange { get; }
        public double MoveSpeed { get; }
        public double ArenaHalfSize { get; }
        public int TicksPerSecond { get; }
        public double DistanceScale { get; }
        public double ApproachWeight { get; }
        public double InterceptWeight { get; }
        public double SpreadWeight { get; }
        public double HoldWeight { get; }
        public double AttackWeight { get; }
        public double InterceptPositionFloor { get; }
        public double PersistenceBonus { get; }
        internal bool IsValid => Positive(InterceptDistance) && Positive(SpreadDistance)
            && Positive(AttackRange) && AiNumber.Weight(MoveSpeed) && Positive(ArenaHalfSize)
            && TicksPerSecond > 0 && Positive(DistanceScale) && AiNumber.Weight(ApproachWeight)
            && AiNumber.Weight(InterceptWeight) && AiNumber.Weight(SpreadWeight)
            && Positive(HoldWeight) && AiNumber.Weight(AttackWeight)
            && InterceptPositionFloor > 0 && InterceptPositionFloor <= 1
            && AiNumber.Weight(PersistenceBonus);
        private static bool Positive(double value) => AiNumber.Weight(value) && value > 0;
    }
}
