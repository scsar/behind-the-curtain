using System;
using System.Collections.Generic;

namespace BehindTheCurtain.Core
{
    public enum AttackScheduleFailureReason
    {
        None,
        InvalidRules,
        InvalidPlan,
        InvalidSchedule,
        TickOverflow,
        TooManyRequiredPoints,
        TooManyOpenCounterWindows
    }

    public readonly struct ScheduledAttackPlan
    {
        public ScheduledAttackPlan(JointAttackPlan plan, long startTick)
        {
            Plan = plan;
            StartTick = startTick;
        }

        public JointAttackPlan Plan { get; }
        public long StartTick { get; }
    }

    /// <summary>One encounter's complete schedule. Validate all plans together before running attacks.</summary>
    public sealed class AttackSchedule
    {
        private AttackSchedule(ScheduledAttackPlan[] plans, JointAttack[] attacks)
        {
            Plans = Array.AsReadOnly(plans);
            Attacks = Array.AsReadOnly(attacks);
        }

        public IReadOnlyList<ScheduledAttackPlan> Plans { get; }
        public IReadOnlyList<JointAttack> Attacks { get; }

        /// <summary>Snapshots the list; empty schedules are valid. Windows include both endpoints.</summary>
        public static bool TryCreate(IReadOnlyList<ScheduledAttackPlan>? plans, FairnessRules? rules,
            out AttackSchedule? schedule, out AttackScheduleFailureReason reason)
        {
            schedule = null;
            reason = AttackScheduleFailureReason.InvalidRules;
            if (rules == null || !rules.IsValid)
                return false;
            reason = AttackScheduleFailureReason.InvalidSchedule;
            if (plans == null)
                return false;

            var snapshot = new ScheduledAttackPlan[plans.Count];
            var attacks = new JointAttack[plans.Count];
            var events = new List<(long Tick, int Delta)>();
            for (var i = 0; i < snapshot.Length; i++)
            {
                var entry = plans[i];
                var plan = entry.Plan;
                reason = AttackScheduleFailureReason.InvalidPlan;
                if (plan == null || plan.CounterWindowTicks < rules.MinCounterWindowTicks)
                    return false;
                reason = AttackScheduleFailureReason.TooManyRequiredPoints;
                if (plan.ConnectionPoints.Count > rules.MaxRequiredPointsPerAttack)
                    return false;
                reason = AttackScheduleFailureReason.TickOverflow;
                if (!plan.TryCreateAttackUnchecked(entry.StartTick, out var attack))
                    return false;
                snapshot[i] = entry;
                attacks[i] = attack!;
                events.Add((attack!.CounterWindow.Start, 1));
                events.Add((attack.CounterWindow.End, -1));
            }

            // Starts precede ends at the same tick: touching inclusive windows overlap.
            events.Sort((a, b) => a.Tick != b.Tick
                ? a.Tick.CompareTo(b.Tick) : b.Delta.CompareTo(a.Delta));
            var open = 0;
            reason = AttackScheduleFailureReason.TooManyOpenCounterWindows;
            foreach (var e in events)
            {
                open += e.Delta;
                if (open > rules.MaxOpenCounterWindows)
                    return false;
            }
            schedule = new AttackSchedule(snapshot, attacks);
            reason = AttackScheduleFailureReason.None;
            return true;
        }
    }
}
