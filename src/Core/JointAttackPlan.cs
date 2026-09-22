using System;
using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    /// <summary>Validated immutable attack configuration. No tactic identity reaches the resolver.</summary>
    public sealed class JointAttackPlan
    {
        // Provisional baseline timing; preparation cost is added exactly once.
        public const long DefaultTelegraphTicks = 20;
        public const long DefaultCounterWindowTicks = 10;

        private readonly FairnessRules rules;

        private JointAttackPlan(long telegraphTicks, long counterWindowTicks, ConnectionPoint[] points, FairnessRules rules)
        {
            this.rules = rules;
            TelegraphTicks = telegraphTicks;
            CounterWindowTicks = counterWindowTicks;
            ConnectionPoints = Array.AsReadOnly(points);
        }

        public long TelegraphTicks { get; }
        public long CounterWindowTicks { get; }
        public IReadOnlyList<ConnectionPoint> ConnectionPoints { get; }

        public static bool TryCreate(Tactic tactic, long baseTelegraphTicks, long counterWindowTicks,
            IReadOnlyList<ConnectionPoint>? points, FairnessRules? rules, out JointAttackPlan? plan)
            => TryCreate(tactic, baseTelegraphTicks, counterWindowTicks, points, rules, out plan, out _);

        public static bool TryCreate(Tactic tactic, long baseTelegraphTicks, long counterWindowTicks,
            IReadOnlyList<ConnectionPoint>? points, FairnessRules? rules, out JointAttackPlan? plan,
            out AttackScheduleFailureReason reason)
        {
            plan = null;
            reason = AttackScheduleFailureReason.InvalidRules;
            if (rules == null || !rules.IsValid)
                return false;
            reason = AttackScheduleFailureReason.InvalidPlan;
            if (counterWindowTicks < rules.MinCounterWindowTicks || baseTelegraphTicks < 0
                || tactic.PreparationTickCost < 0
                || baseTelegraphTicks > long.MaxValue - tactic.PreparationTickCost
                || points == null || points.Count == 0)
                return false;

            var snapshot = new ConnectionPoint[points.Count];
            var seen = new HashSet<ConnectionPoint>();
            for (var i = 0; i < snapshot.Length; i++)
            {
                var point = points[i];
                if (point == null || !point.IsCatalogPoint || !seen.Add(point))
                    return false;
                snapshot[i] = point;
            }
            if (points.Count > rules.MaxRequiredPointsPerAttack)
            {
                reason = AttackScheduleFailureReason.TooManyRequiredPoints;
                return false;
            }
            plan = new JointAttackPlan(baseTelegraphTicks + tactic.PreparationTickCost,
                counterWindowTicks, snapshot, rules);
            reason = AttackScheduleFailureReason.None;
            return true;
        }

        /// <summary>Window length counts inclusive ticks; execution is the next tick after its end.</summary>
        public bool TryCreateAttack(long startTick, out JointAttack? attack)
        {
            attack = null;
            if (!AttackSchedule.TryCreate(new[] { new ScheduledAttackPlan(this, startTick) },
                rules, out var schedule, out _))
                return false;
            attack = schedule!.Attacks[0];
            return true;
        }

        internal bool TryCreateAttackUnchecked(long startTick, out JointAttack? attack)
        {
            attack = null;
            if (startTick > long.MaxValue - TelegraphTicks)
                return false;
            var windowStart = startTick + TelegraphTicks;
            if (windowStart > long.MaxValue - CounterWindowTicks)
                return false;
            var executionTick = windowStart + CounterWindowTicks;
            TickWindow.TryCreate(windowStart, executionTick - 1, out var window);
            return JointAttack.TryCreate(startTick, window, executionTick, ConnectionPoints, out attack);
        }
    }
}
