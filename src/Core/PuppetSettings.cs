using System.Collections.Generic;
using System.Collections.ObjectModel;

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
            double persistenceBonus = 0.02, double threatRadius = 6, double keepMin = 5,
            double keepMax = 8, double weightJitter = 0.15, int reevaluationIntervalTicks = 30,
            double keepDistanceWeight = 1.2, double passiveInterceptScale = 0.02, double keepDistanceSpeed = 6,
            double attackArrivalMargin = 0.05, double guardRearOffset = 2,
            double guardHoldScale = 3,
            IReadOnlyDictionary<TargetRole, IReadOnlyDictionary<string, double>>? roleWeights = null, double individualThreatInterceptScale = 0.5)
        {
            IndividualThreatInterceptScale = individualThreatInterceptScale;
            GuardRearOffset = guardRearOffset; GuardHoldScale = guardHoldScale;
            KeepDistanceSpeed = keepDistanceSpeed; AttackArrivalMargin = attackArrivalMargin;
            ThreatRadius = threatRadius; KeepMin = keepMin; KeepMax = keepMax;
            WeightJitter = weightJitter; ReevaluationIntervalTicks = reevaluationIntervalTicks;
            KeepDistanceWeight = keepDistanceWeight; PassiveInterceptScale = passiveInterceptScale;
            InterceptDistance = interceptDistance; SpreadDistance = spreadDistance;
            AttackRange = attackRange; MoveSpeed = moveSpeed; ArenaHalfSize = arenaHalfSize;
            TicksPerSecond = ticksPerSecond; DistanceScale = distanceScale;
            ApproachWeight = approachWeight; InterceptWeight = interceptWeight;
            SpreadWeight = spreadWeight; HoldWeight = holdWeight; AttackWeight = attackWeight;
            InterceptPositionFloor = interceptPositionFloor; PersistenceBonus = persistenceBonus;
            var tables = new Dictionary<TargetRole, IReadOnlyDictionary<string, double>>();
            foreach (TargetRole role in System.Enum.GetValues(typeof(TargetRole)))
            {
                var row = new Dictionary<string, double> {
                    [ActionIds.Waver] = 0,
                    [ActionIds.Approach] = role == TargetRole.Healer ? 0 : approachWeight,
                    [ActionIds.Attack] = role == TargetRole.Healer ? 0 : attackWeight,
                    [ActionIds.Hold] = role == TargetRole.Attacker ? 0 : holdWeight * guardHoldScale,
                    [ActionIds.Intercept] = role == TargetRole.Guard ? interceptWeight : 0,
                    [ActionIds.Spread] = role == TargetRole.Guard ? 0 : spreadWeight,
                    [ActionIds.KeepDistance] = role == TargetRole.Healer ? keepDistanceWeight : 0
                };
                if (roleWeights != null && roleWeights.TryGetValue(role, out var entries))
                    foreach (var entry in entries)
                        if (entry.Key != ActionIds.Waver) row[entry.Key] = entry.Value;
                tables[role] = new ReadOnlyDictionary<string, double>(row);
            }
            RoleWeights = new ReadOnlyDictionary<TargetRole, IReadOnlyDictionary<string, double>>(tables);
        }
        public double GuardRearOffset { get; }
        public double GuardHoldScale { get; }
        public double KeepDistanceSpeed { get; }
        public double AttackArrivalMargin { get; }
        public double ThreatRadius { get; }
        public double KeepMin { get; }
        public double KeepMax { get; }
        public double WeightJitter { get; }
        public int ReevaluationIntervalTicks { get; }
        public double KeepDistanceWeight { get; }
        // Provisional, arbitrarily chosen individual threat weight.
        public double IndividualThreatInterceptScale { get; }
        public double PassiveInterceptScale { get; }
        // Provisional role table. Zero disables the default, but explicit role intentions can enable it.
        public IReadOnlyDictionary<TargetRole, IReadOnlyDictionary<string, double>> RoleWeights { get; }
        public double GetRoleWeight(TargetRole role, string action) =>
            action != ActionIds.Waver && RoleWeights.TryGetValue(role, out var row) && row.TryGetValue(action, out var weight) ? weight : 0;
        private bool ValidRoleWeights
        {
            get
            {
                foreach (var row in RoleWeights.Values)
                    foreach (var weight in row.Values) if (!AiNumber.Weight(weight)) return false;
                return true;
            }
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
        internal bool IsValid => AiNumber.Weight(IndividualThreatInterceptScale) && ValidRoleWeights && Positive(GuardRearOffset) && Positive(GuardHoldScale) && Positive(KeepDistanceSpeed) && AttackArrivalMargin >= 0
            && AttackArrivalMargin < AttackRange && Positive(ThreatRadius) && Positive(KeepMin) && KeepMax > KeepMin
            && AiNumber.Finite(KeepMax) && WeightJitter >= 0 && WeightJitter < 1
            && ReevaluationIntervalTicks > 0 && Positive(KeepDistanceWeight) && AiNumber.Weight(PassiveInterceptScale)
            && Positive(InterceptDistance) && Positive(SpreadDistance)
            && Positive(AttackRange) && AiNumber.Weight(MoveSpeed) && Positive(ArenaHalfSize)
            && TicksPerSecond > 0 && Positive(DistanceScale) && AiNumber.Weight(ApproachWeight)
            && AiNumber.Weight(InterceptWeight) && AiNumber.Weight(SpreadWeight)
            && Positive(HoldWeight) && AiNumber.Weight(AttackWeight)
            && InterceptPositionFloor > 0 && InterceptPositionFloor <= 1
            && AiNumber.Weight(PersistenceBonus);
        private static bool Positive(double value) => AiNumber.Weight(value) && value > 0;
    }
}
