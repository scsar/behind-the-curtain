using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BehindTheCurtain.Core
{
    /// <summary>Provisional, arbitrarily chosen distances, speeds and utility tuning for T15; reevaluation interval: S2 탐색 + U7 Play, 2026-11-08.</summary>
    public sealed class PuppetSettings
    {
        public PuppetSettings(double interceptDistance = 1.5, double spreadDistance = 3,
            double attackRange = 2, double moveSpeed = 3, double arenaHalfSize = 10,
            int ticksPerSecond = 30, double distanceScale = 10, double approachWeight = 0.2,
            double interceptWeight = 1.5, double spreadWeight = 0.15, double holdWeight = 0.1,
            double attackWeight = 0.8, double interceptPositionFloor = 0.6,
            double persistenceBonus = 0.02, double threatRadius = 6, double keepMin = 5,
            double keepMax = 8, double weightJitter = 0.15, int reevaluationIntervalTicks = 10,
            double keepDistanceWeight = 1.2, double passiveInterceptScale = 0.02, double keepDistanceSpeed = 6,
            double attackArrivalMargin = 0.05, double guardRearOffset = 2,
            double guardHoldScale = 3,
            IReadOnlyDictionary<TargetRole, IReadOnlyDictionary<string, double>>? roleWeights = null, double individualThreatInterceptScale = 0.5,
            double midAttackRange = 3.5, double rangedAttackRange = 8, double interceptStandOff = 1.0,
            double interceptHoldTolerance = 0.3, double interceptClearance = 1.0,
            double interceptWaypointTolerance = 0.2, double targetDistanceScale = 4, int pressCap = 2,
            double crowdPenalty = 0.3, double targetSwitchMargin = 1.3, int targetMinHoldTicks = 30)
            : this(new BasicActionSettings(), interceptDistance, spreadDistance, attackRange, moveSpeed, arenaHalfSize, ticksPerSecond, distanceScale, approachWeight, interceptWeight, spreadWeight, holdWeight, attackWeight, interceptPositionFloor, persistenceBonus, threatRadius, keepMin, keepMax, weightJitter, reevaluationIntervalTicks, keepDistanceWeight, passiveInterceptScale, keepDistanceSpeed, attackArrivalMargin, guardRearOffset, guardHoldScale, roleWeights, individualThreatInterceptScale, midAttackRange, rangedAttackRange, interceptStandOff, interceptHoldTolerance, interceptClearance, interceptWaypointTolerance, targetDistanceScale, pressCap, crowdPenalty, targetSwitchMargin, targetMinHoldTicks) { }

        public PuppetSettings(BasicActionSettings basicActions, double interceptDistance = 1.5, double spreadDistance = 3,
            double attackRange = 2, double moveSpeed = 3, double arenaHalfSize = 10,
            int ticksPerSecond = 30, double distanceScale = 10, double approachWeight = 0.2,
            double interceptWeight = 1.5, double spreadWeight = 0.15, double holdWeight = 0.1,
            double attackWeight = 0.8, double interceptPositionFloor = 0.6,
            double persistenceBonus = 0.02, double threatRadius = 6, double keepMin = 5,
            double keepMax = 8, double weightJitter = 0.15, int reevaluationIntervalTicks = 10,
            double keepDistanceWeight = 1.2, double passiveInterceptScale = 0.02, double keepDistanceSpeed = 6,
            double attackArrivalMargin = 0.05, double guardRearOffset = 2,
            double guardHoldScale = 3,
            IReadOnlyDictionary<TargetRole, IReadOnlyDictionary<string, double>>? roleWeights = null, double individualThreatInterceptScale = 0.5,
            double midAttackRange = 3.5, double rangedAttackRange = 8, double interceptStandOff = 1.0,
            double interceptHoldTolerance = 0.3, double interceptClearance = 1.0,
            double interceptWaypointTolerance = 0.2, double targetDistanceScale = 4, int pressCap = 2,
            double crowdPenalty = 0.3, double targetSwitchMargin = 1.3, int targetMinHoldTicks = 30)
        {
            TargetDistanceScale = targetDistanceScale; PressCap = pressCap; CrowdPenalty = crowdPenalty;
            TargetSwitchMargin = targetSwitchMargin; TargetMinHoldTicks = targetMinHoldTicks;
            FlankAngle = basicActions?.FlankAngle ?? double.NaN; FlankRadius = basicActions?.FlankRadius ?? double.NaN;
            HarassMargin = basicActions?.HarassMargin ?? double.NaN;
            HarassWithdrawMaxTicks = basicActions?.HarassWithdrawMaxTicks ?? 0;
            HarassIntervalTicks = basicActions?.HarassIntervalTicks ?? 0; HarassRecoverTicks = basicActions?.HarassRecoverTicks ?? 0;
            FlankNonMeleeScale = basicActions?.FlankNonMeleeScale ?? double.NaN; HarassMeleeScale = basicActions?.HarassMeleeScale ?? double.NaN;
            ApproachNonMeleeScale = basicActions?.ApproachNonMeleeScale ?? double.NaN;
            KeepAlignTolerance = basicActions?.KeepAlignTolerance ?? double.NaN;
            double flankWeight = basicActions?.FlankWeight ?? double.NaN, harassWeight = basicActions?.HarassWeight ?? double.NaN;
            InterceptStandOff = interceptStandOff; InterceptHoldTolerance = interceptHoldTolerance;
            InterceptClearance = interceptClearance; InterceptWaypointTolerance = interceptWaypointTolerance;
            MidAttackRange = midAttackRange; RangedAttackRange = rangedAttackRange;
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
                    [ActionIds.Surround] = role == TargetRole.Attacker ? basicActions?.SurroundWeight ?? double.NaN
                        : role == TargetRole.Guard ? basicActions?.SurroundGuardWeight ?? double.NaN : 0,
                    [ActionIds.Flank] = role == TargetRole.Attacker ? flankWeight : 0,
                    [ActionIds.Harass] = role == TargetRole.Attacker ? harassWeight : 0,
                    [ActionIds.Waver] = 0,
                    [ActionIds.Pressure] = 0,
                    [ActionIds.Approach] = role == TargetRole.Healer ? 0 : approachWeight,
                    [ActionIds.Attack] = role == TargetRole.Healer ? 0 : attackWeight,
                    [ActionIds.Hold] = role == TargetRole.Attacker ? 0 : holdWeight * guardHoldScale,
                    [ActionIds.Intercept] = role == TargetRole.Guard ? interceptWeight : 0,
                    [ActionIds.Spread] = role == TargetRole.Guard ? 0 : spreadWeight,
                    [ActionIds.KeepDistance] = role == TargetRole.Healer ? keepDistanceWeight : 0
                };
                if (roleWeights != null && roleWeights.TryGetValue(role, out var entries))
                    foreach (var entry in entries)
                        if (entry.Key != ActionIds.Waver && entry.Key != ActionIds.Pressure) row[entry.Key] = entry.Value;
                tables[role] = new ReadOnlyDictionary<string, double>(row);
            }
            RoleWeights = new ReadOnlyDictionary<TargetRole, IReadOnlyDictionary<string, double>>(tables);
        }
        // Preserve the original constructor signature for compiled callers.
        public PuppetSettings(double interceptDistance, double spreadDistance,
            double attackRange, double moveSpeed, double arenaHalfSize,
            int ticksPerSecond, double distanceScale, double approachWeight,
            double interceptWeight, double spreadWeight, double holdWeight,
            double attackWeight, double interceptPositionFloor,
            double persistenceBonus, double threatRadius, double keepMin,
            double keepMax, double weightJitter, int reevaluationIntervalTicks,
            double keepDistanceWeight, double passiveInterceptScale, double keepDistanceSpeed,
            double attackArrivalMargin, double guardRearOffset,
            double guardHoldScale,
            IReadOnlyDictionary<TargetRole, IReadOnlyDictionary<string, double>>? roleWeights, double individualThreatInterceptScale,
            double midAttackRange, double rangedAttackRange, double interceptStandOff,
            double interceptHoldTolerance, double interceptClearance,
            double interceptWaypointTolerance)
            : this(interceptDistance, spreadDistance, attackRange, moveSpeed, arenaHalfSize, ticksPerSecond, distanceScale, approachWeight, interceptWeight, spreadWeight, holdWeight, attackWeight, interceptPositionFloor, persistenceBonus, threatRadius, keepMin, keepMax, weightJitter, reevaluationIntervalTicks, keepDistanceWeight, passiveInterceptScale, keepDistanceSpeed, attackArrivalMargin, guardRearOffset, guardHoldScale, roleWeights, individualThreatInterceptScale, midAttackRange, rangedAttackRange, interceptStandOff, interceptHoldTolerance, interceptClearance, interceptWaypointTolerance, 4, 2, 0.3, 1.3, 30) { }

        // Preserve the original constructor signature for compiled callers.
        public PuppetSettings(BasicActionSettings basicActions, double interceptDistance, double spreadDistance,
            double attackRange, double moveSpeed, double arenaHalfSize,
            int ticksPerSecond, double distanceScale, double approachWeight,
            double interceptWeight, double spreadWeight, double holdWeight,
            double attackWeight, double interceptPositionFloor,
            double persistenceBonus, double threatRadius, double keepMin,
            double keepMax, double weightJitter, int reevaluationIntervalTicks,
            double keepDistanceWeight, double passiveInterceptScale, double keepDistanceSpeed,
            double attackArrivalMargin, double guardRearOffset,
            double guardHoldScale,
            IReadOnlyDictionary<TargetRole, IReadOnlyDictionary<string, double>>? roleWeights, double individualThreatInterceptScale,
            double midAttackRange, double rangedAttackRange, double interceptStandOff,
            double interceptHoldTolerance, double interceptClearance,
            double interceptWaypointTolerance)
            : this(basicActions, interceptDistance, spreadDistance, attackRange, moveSpeed, arenaHalfSize, ticksPerSecond, distanceScale, approachWeight, interceptWeight, spreadWeight, holdWeight, attackWeight, interceptPositionFloor, persistenceBonus, threatRadius, keepMin, keepMax, weightJitter, reevaluationIntervalTicks, keepDistanceWeight, passiveInterceptScale, keepDistanceSpeed, attackArrivalMargin, guardRearOffset, guardHoldScale, roleWeights, individualThreatInterceptScale, midAttackRange, rangedAttackRange, interceptStandOff, interceptHoldTolerance, interceptClearance, interceptWaypointTolerance, 4, 2, 0.3, 1.3, 30) { }

        /// <summary>Provisional, arbitrarily chosen temperament tuning.</summary>
        public TemperamentSettings TemperamentTuning { get; private set; } = new TemperamentSettings();
        public PuppetSettings WithTemperamentTuning(TemperamentSettings tuning)
        {
            var copy = (PuppetSettings)MemberwiseClone();
            copy.TemperamentTuning = tuning;
            return copy;
        }
        internal double WhimPressureExponent { get; private set; }
        internal double PressureFactor(int otherPressure) => WhimPressureExponent == 0 ? 1
            : System.Math.Pow(1.0 / (1 + otherPressure), WhimPressureExponent);

        internal PuppetSettings ApplyTemperament(Temperament temperament)
        {
            // Preserve the exact original settings object for every zero/default temperament.
            if (temperament.IsZero) return this;
            var copy = (PuppetSettings)MemberwiseClone();
            double b = temperament[TemperamentAxisIds.Boldness], p = temperament[TemperamentAxisIds.Persistence];
            double distance = TemperamentTuning.TemperamentDistanceScale * b;
            copy.InterceptDistance *= 1 + distance;
            copy.InterceptStandOff *= 1 - distance;
            // BasicActionSettings values are already flattened into this immutable snapshot.
            copy.HarassMargin *= 1 - distance;
            copy.KeepMin *= 1 - distance;
            copy.KeepMax *= 1 - distance;
            double hold = 1 + TemperamentTuning.TemperamentPersistenceScale * p;
            copy.TargetSwitchMargin = 1 + (TargetSwitchMargin - 1) * hold;
            copy.TargetMinHoldTicks = (int)System.Math.Round(TargetMinHoldTicks * hold);
            copy.WhimPressureExponent = p < 0 ? TemperamentTuning.WhimPressureWeight * -p : 0;
            return copy;
        }

        // Provisional, arbitrarily chosen target selection tuning (metres and ticks).
        public double TargetDistanceScale { get; }
        public int PressCap { get; }
        public double CrowdPenalty { get; }
        public double TargetSwitchMargin { get; private set; }
        public int TargetMinHoldTicks { get; private set; }
        // Provisional, arbitrarily chosen T16b-3a-1 tuning (degrees, metres, ticks).
        public double ApproachNonMeleeScale { get; }
        public double KeepAlignTolerance { get; }
        public double FlankAngle { get; }
        public double FlankRadius { get; }
        public double HarassMargin { get; private set; }
        public int HarassIntervalTicks { get; }
        public int HarassRecoverTicks { get; }
        public int HarassWithdrawMaxTicks { get; }
        public double FlankNonMeleeScale { get; }
        public double HarassMeleeScale { get; }
        public double GuardRearOffset { get; }
        public double GuardHoldScale { get; }
        public double KeepDistanceSpeed { get; }
        public double AttackArrivalMargin { get; }
        public double ThreatRadius { get; }
        public double KeepMin { get; private set; }
        public double KeepMax { get; private set; }
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
        // Provisional, arbitrarily chosen intercept geometry tolerances (metres).
        public double InterceptStandOff { get; private set; }
        public double InterceptHoldTolerance { get; }
        public double InterceptClearance { get; }
        public double InterceptWaypointTolerance { get; }
        public double InterceptDistance { get; private set; }
        public double SpreadDistance { get; }
        public double AttackRange { get; }
        // Provisional, arbitrarily chosen T16b-1 distances.
        public double MidAttackRange { get; }
        public double RangedAttackRange { get; }
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
        internal bool IsValid => TemperamentTuning != null && TemperamentTuning.IsValid && Positive(TargetDistanceScale) && PressCap > 0
            && Positive(CrowdPenalty) && CrowdPenalty <= 1 && Positive(TargetSwitchMargin)
            && TargetSwitchMargin >= 1 && TargetMinHoldTicks >= 0 && AiNumber.Weight(ApproachNonMeleeScale)
            && AiNumber.Weight(KeepAlignTolerance) && KeepAlignTolerance <= 180 && Positive(FlankAngle) && FlankAngle <= 180 && Positive(FlankRadius)
            && Positive(HarassMargin) && HarassIntervalTicks > 0 && HarassRecoverTicks > 0 && HarassWithdrawMaxTicks > 0
            && AiNumber.Weight(FlankNonMeleeScale) && AiNumber.Weight(HarassMeleeScale) && AiNumber.Weight(IndividualThreatInterceptScale) && ValidRoleWeights && Positive(GuardRearOffset) && Positive(GuardHoldScale) && Positive(KeepDistanceSpeed) && AttackArrivalMargin >= 0
            && Positive(MidAttackRange) && Positive(RangedAttackRange)
            && AttackArrivalMargin < MidAttackRange && AttackArrivalMargin < RangedAttackRange
            && AttackArrivalMargin < AttackRange && Positive(ThreatRadius) && Positive(KeepMin) && KeepMax > KeepMin
            && AiNumber.Finite(KeepMax) && WeightJitter >= 0 && WeightJitter < 1
            && ReevaluationIntervalTicks > 0 && Positive(KeepDistanceWeight) && AiNumber.Weight(PassiveInterceptScale)
            && Positive(InterceptStandOff) && AiNumber.Weight(InterceptHoldTolerance)
            && Positive(InterceptClearance) && Positive(InterceptWaypointTolerance) && Positive(InterceptDistance) && Positive(SpreadDistance)
            && Positive(AttackRange) && AiNumber.Weight(MoveSpeed) && Positive(ArenaHalfSize)
            && TicksPerSecond > 0 && Positive(DistanceScale) && AiNumber.Weight(ApproachWeight)
            && AiNumber.Weight(InterceptWeight) && AiNumber.Weight(SpreadWeight)
            && Positive(HoldWeight) && AiNumber.Weight(AttackWeight)
            && InterceptPositionFloor > 0 && InterceptPositionFloor <= 1
            && AiNumber.Weight(PersistenceBonus);
        private static bool Positive(double value) => AiNumber.Weight(value) && value > 0;
    }
}
