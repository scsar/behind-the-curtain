using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class AttackScheduleTests
{
    [Theory]
    [InlineData(0, 0, 10, true)] // Two overlap, third starts after their ends.
    [InlineData(0, 0, 0, false)]
    [InlineData(0, 0, 9, false)] // End == start counts as three.
    [InlineData(0, 10, 20, true)]
    [InlineData(20, 0, 10, true)] // Input need not be sorted.
    public void InclusiveWindowBudget(long a, long b, long c, bool expected)
    {
        var plan = Plan();
        var entries = new[] { a, b, c }.Select(t => new ScheduledAttackPlan(plan, t)).ToArray();
        Assert.Equal(expected, AttackSchedule.TryCreate(entries, new FairnessRules(), out var schedule, out var reason));
        Assert.Equal(expected ? AttackScheduleFailureReason.None
            : AttackScheduleFailureReason.TooManyOpenCounterWindows, reason);
        Assert.Equal(expected, schedule != null);
    }

    [Fact]
    public void RequiredPointsAreRejectedAtPlanCreationAndRecheckedBySchedule()
    {
        var points = Enumerable.Range(0, 3).Select(_ =>
            new ConnectionPoint(ConnectionPointKind.Thread, ConnectionPointPosition.Front)).ToArray();
        Assert.False(JointAttackPlan.TryCreate(default, 0, 10, points, new FairnessRules(), out var invalid, out var reason));
        Assert.Null(invalid);
        Assert.Equal(AttackScheduleFailureReason.TooManyRequiredPoints, reason);
        var relaxed = new FairnessRules(maxRequiredPointsPerAttack: 3);
        Assert.True(JointAttackPlan.TryCreate(default, 0, 10, points, relaxed, out var plan, out _));
        var entries = new[] { new ScheduledAttackPlan(plan!, 0) };
        Assert.False(AttackSchedule.TryCreate(entries, new FairnessRules(), out _, out reason));
        Assert.Equal(AttackScheduleFailureReason.TooManyRequiredPoints, reason);
        Assert.True(AttackSchedule.TryCreate(entries, relaxed, out _, out _));
        Assert.True(plan!.TryCreateAttack(0, out _));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void OpenWindowLimitIsConfigurable(int limit, bool expected)
    {
        var entries = Enumerable.Repeat(new ScheduledAttackPlan(Plan(), 0), 3).ToArray();
        Assert.Equal(expected, AttackSchedule.TryCreate(entries,
            new FairnessRules(maxOpenCounterWindows: limit), out _, out _));
    }

    [Fact]
    public void TwoTouchingWindowsExceedLimitOfOne()
    {
        var plan = Plan();
        Assert.False(AttackSchedule.TryCreate(new[] { new ScheduledAttackPlan(plan, 0),
            new ScheduledAttackPlan(plan, 9) }, new FairnessRules(maxOpenCounterWindows: 1), out _, out var reason));
        Assert.Equal(AttackScheduleFailureReason.TooManyOpenCounterWindows, reason);
    }

    [Fact]
    public void InvalidInputsAndExtremeTicksReturnReasons()
    {
        var entry = new[] { new ScheduledAttackPlan(Plan(), 0) };
        foreach (var rules in new FairnessRules?[] { null, new(0), new(maxOpenCounterWindows: 0),
            new(maxOpenCounterWindows: -1), new(maxRequiredPointsPerAttack: 0), new(maxRequiredPointsPerAttack: -1) })
        {
            Assert.False(AttackSchedule.TryCreate(entry, rules, out var failed, out var reason));
            Assert.Null(failed);
            Assert.Equal(AttackScheduleFailureReason.InvalidRules, reason);
        }
        Assert.False(AttackSchedule.TryCreate(null, new(), out _, out var failure));
        Assert.Equal(AttackScheduleFailureReason.InvalidSchedule, failure);
        Assert.False(AttackSchedule.TryCreate(new ScheduledAttackPlan[1], new(), out _, out failure));
        Assert.Equal(AttackScheduleFailureReason.InvalidPlan, failure);
        Assert.False(AttackSchedule.TryCreate(entry, new(11), out _, out failure));
        Assert.Equal(AttackScheduleFailureReason.InvalidPlan, failure);
        Assert.False(AttackSchedule.TryCreate(new[] { new ScheduledAttackPlan(Plan(), long.MaxValue) },
            new(), out _, out failure));
        Assert.Equal(AttackScheduleFailureReason.TickOverflow, failure);
        Assert.True(AttackSchedule.TryCreate(new[] { new ScheduledAttackPlan(Plan(), long.MinValue),
            new ScheduledAttackPlan(Plan(), long.MaxValue - 10) }, new(), out _, out _));
        Assert.True(AttackSchedule.TryCreate(Array.Empty<ScheduledAttackPlan>(), new(), out _, out _));
    }

    [Fact]
    public void ScheduleSnapshotsInputAndKeepsIndependentAttackState()
    {
        var entries = new[] { new ScheduledAttackPlan(Plan(), 0) };
        Assert.True(AttackSchedule.TryCreate(entries, new(), out var schedule, out _));
        Assert.True(AttackSchedule.TryCreate(entries, new(), out var replay, out _));
        entries[0] = default;
        Assert.NotNull(schedule!.Plans[0].Plan);
        Assert.Throws<NotSupportedException>(() => ((IList<ScheduledAttackPlan>)schedule.Plans)[0] = default);
        Assert.True(schedule.Attacks[0].Advance(10));
        Assert.Equal(JointAttackState.Telegraphing, replay!.Attacks[0].State);
    }

    [Fact]
    public void AllTacticsProfilesAndSeedsPreserveBudgetAndReplay()
    {
        var selector = new RuleTacticSelector();
        var tactics = new[] { default(BehaviorProfile), Profile(4, 0, 0), Profile(0, 4, 0), Profile(0, 0, 4) }
            .Select(selector.Select).ToArray();
        Assert.Equal(Enum.GetValues<TacticId>().Length, tactics.Select(t => t.Id).Distinct().Count());
        foreach (var seed in new[] { int.MinValue, -1, 0, 1, 42, int.MaxValue })
        {
            var random = new Random(seed);
            for (var sample = 0; sample < 20; sample++)
            foreach (var tactic in tactics.Append(selector.Select(Profile(random.Next(5), random.Next(5), random.Next(5)))))
            {
                Assert.True(tactic.TryCreatePlan(out var plan));
                foreach (var limit in new[] { 1, 2, 3 })
                {
                    var rules = new FairnessRules(maxOpenCounterWindows: limit,
                        maxRequiredPointsPerAttack: Tactic.RequiredConnectionPointCount);
                    var entries = Enumerable.Range(0, 6).Select(_ => new ScheduledAttackPlan(plan!, random.Next(-30, 31))).ToArray();
                    // Independent tick-by-tick oracle over the generated range.
                    var peak = Enumerable.Range(-30, 200).Max(t => entries.Count(e =>
                        t >= e.StartTick + e.Plan.TelegraphTicks &&
                        t < e.StartTick + e.Plan.TelegraphTicks + e.Plan.CounterWindowTicks));
                    var accepted = AttackSchedule.TryCreate(entries, rules, out var schedule, out var reason);
                    Assert.Equal(peak <= limit, accepted);
                    Assert.Equal(accepted, AttackSchedule.TryCreate(entries, rules, out var replay, out var replayReason));
                    Assert.Equal(reason, replayReason);
                    if (!accepted)
                    {
                        Assert.Null(schedule);
                        Assert.Equal(AttackScheduleFailureReason.TooManyOpenCounterWindows, reason);
                        continue;
                    }
                    Assert.All(schedule!.Attacks, a => Assert.InRange(a.ConnectionPoints.Count, 1, rules.MaxRequiredPointsPerAttack));
                    Assert.Equal(schedule.Attacks.Select(a => a.CounterWindow), replay!.Attacks.Select(a => a.CounterWindow));
                }
            }
        }
    }

    private static JointAttackPlan Plan()
    {
        Assert.True(JointAttackPlan.TryCreate(default, 0, 10,
            new[] { new ConnectionPoint(ConnectionPointKind.Thread, ConnectionPointPosition.Front) }, new(), out var plan));
        return plan!;
    }

    private static BehaviorProfile Profile(int area, int healer, int ranged)
    {
        Assert.True(CombatObserver.TryCreate(new ObserverSettings(sustainedRangedStreak: 1), out var observer));
        for (var i = 0; i < 4; i++)
            Assert.True(observer!.Observe(new AttackEvent(i < area ? 2 : 0,
                i < healer ? TargetRole.Healer : TargetRole.Guard,
                i < ranged ? AttackRange.Ranged : AttackRange.Melee, i)));
        return observer!.EndEncounter();
    }
}
