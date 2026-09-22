using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class JointAttackPlanTests
{
    [Fact]
    public void TacticLayoutsChangeExposureAndUseConfiguredPreparationCosts()
    {
        Assert.True(RuleTacticSelector.TryCreate(new TacticSettings(
            spreadFormationPreparationTickCost: 21, guardHealerPreparationTickCost: 22,
            shieldAdvancePreparationTickCost: 23, standardFormationPreparationTickCost: 24), out var selector));
        var profiles = new[] { Profile(4, 0, 0), Profile(0, 4, 0), Profile(0, 0, 4), default };
        var counts = new[] { 2, 2, 1, 1 }; // Provisional layouts, not fairness requirements.
        var positions = new[] { ConnectionPointPosition.Left, ConnectionPointPosition.Rear,
            ConnectionPointPosition.Right, ConnectionPointPosition.Front };
        for (var i = 0; i < profiles.Length; i++)
        {
            Assert.True(selector!.Select(profiles[i]).TryCreatePlan(out var plan));
            Assert.Equal(JointAttackPlan.DefaultTelegraphTicks + 21 + i, plan!.TelegraphTicks);
            Assert.Equal(counts[i], plan.ConnectionPoints.Count);
            Assert.Equal(positions[i], plan.ConnectionPoints[0].Position);
            Assert.Equal(JointAttackPlan.DefaultCounterWindowTicks, plan.CounterWindowTicks);
        }
    }

    [Fact]
    public void AllTacticsAcrossProfilesAndSeededInputs_PreserveFairnessAndReplay()
    {
        var tactics = Tactics();
        Assert.Equal(Enum.GetValues<TacticId>().Length, tactics.Select(t => t.Id).Distinct().Count());
        foreach (var seed in new[] { int.MinValue, -1, 0, 1, 42, int.MaxValue })
        {
            var random = new Random(seed);
            for (var sample = 0; sample < 20; sample++)
            {
                var profile = Profile(random.Next(5), random.Next(5), random.Next(5));
                var selected = new RuleTacticSelector().Select(profile);
                // Exercise every tactic for each profile, as well as the actual selector path.
                foreach (var tactic in tactics.Append(selected))
                {
                    Assert.True(tactic.TryCreatePlan(out var plan));
                    Assert.NotNull(plan);
                    Assert.True(tactic.TryCreatePlan(out var replay));
                    Assert.Equal(JointAttackPlan.DefaultTelegraphTicks + tactic.PreparationTickCost,
                        plan.TelegraphTicks);
                    Assert.True(plan.CounterWindowTicks >= FairnessRules.DefaultMinCounterWindowTicks);
                    Assert.NotEmpty(plan.ConnectionPoints);
                    Assert.Equal(plan.TelegraphTicks, replay!.TelegraphTicks);
                    Assert.Equal(plan.CounterWindowTicks, replay.CounterWindowTicks);
                    Assert.Equal(plan.ConnectionPoints.Select(Signature), replay.ConnectionPoints.Select(Signature));
                    foreach (var point in plan.ConnectionPoints)
                    {
                        Assert.True(ConnectionPointCatalog.TryGetAllowedMethods(point.Kind!.Value, out var methods));
                        Assert.Equal(methods, point.AllowedMethods);
                    }
                    var start = random.Next(-1000, 1000);
                    Assert.True(plan.TryCreateAttack(start, out var attack));
                    Assert.Equal(start + plan.TelegraphTicks, attack!.CounterWindow.Start);
                    Assert.Equal(plan.CounterWindowTicks, attack.CounterWindow.End - attack.CounterWindow.Start + 1);
                    Assert.Equal(attack.CounterWindow.End + 1, attack.ExecutionTick);
                }
            }
        }
    }

    [Fact]
    public void SameAttemptAndAttackState_HaveSameResultForEveryTactic()
    {
        // Align absolute windows while retaining tactic-specific preparation and layouts.
        foreach (var method in new[] { (InterferenceMethod)0, InterferenceMethod.Cut,
            InterferenceMethod.Shoot, InterferenceMethod.Disrupt, (InterferenceMethod)3, (InterferenceMethod)8 })
        foreach (var tick in new long[] { 99, 100, 105, 109, 110 })
        foreach (var state in new[] { "telegraph", "open", "stale", "executed", "broken" })
        foreach (var targetMode in new[] { "exposed", "foreign", "null" })
        {
            CounterFailureReason? baseline = null;
            foreach (var tactic in Tactics())
            {
                Assert.True(tactic.TryCreatePlan(out var plan));
                Assert.True(plan!.TryCreateAttack(100 - plan.TelegraphTicks, out var attack));
                var point = attack!.ConnectionPoints[0];
                Assert.Equal(ConnectionPointKind.Thread, point.Kind);
                switch (state)
                {
                    case "open": Assert.True(attack.Advance(100)); break;
                    case "stale": Assert.True(attack.Advance(106)); break;
                    case "executed": Assert.True(attack.Advance(110)); break;
                    case "broken":
                        Assert.True(CounterResolver.Resolve(attack, 100, InterferenceMethod.Cut, point).Success);
                        break;
                }
                var target = targetMode == "exposed" ? point : targetMode == "null" ? null
                    : new ConnectionPoint(ConnectionPointKind.Thread, point.Position);
                var result = CounterResolver.Resolve(attack, tick, method, target);
                var expected = state == "broken" ? CounterFailureReason.AlreadyUsed
                    : state == "executed" || tick < 100 || tick > 109 || (state == "stale" && tick < 106)
                        ? CounterFailureReason.OutsideWindow
                    : targetMode != "exposed" ? CounterFailureReason.ConnectionPointNotFound
                    : method != InterferenceMethod.Cut ? CounterFailureReason.MethodMismatch
                    : CounterFailureReason.None;
                Assert.Equal(expected, result.FailureReason);
                baseline ??= result.FailureReason;
                Assert.Equal(baseline.Value, result.FailureReason);
            }
        }
    }

    [Fact]
    public void Creation_RejectsInvalidFairnessInputs()
    {
        var point = new ConnectionPoint(ConnectionPointKind.Thread, ConnectionPointPosition.Front);
        IReadOnlyList<ConnectionPoint>?[] invalid = {
            null, Array.Empty<ConnectionPoint>(), new ConnectionPoint[] { null! },
            new[] { point, point }, new[] { new ConnectionPoint(InterferenceMethod.Cut) },
            new[] { new ConnectionPoint((ConnectionPointKind)999, ConnectionPointPosition.Front) },
            new[] { new ConnectionPoint(ConnectionPointKind.Thread, (ConnectionPointPosition)999) }
        };
        foreach (var points in invalid)
            Reject(20, 10, points, new FairnessRules());
        foreach (var length in new long[] { long.MinValue, -1, 0, 9 })
            Reject(20, length, new[] { point }, new FairnessRules());
        Reject(-1, 10, new[] { point }, new FairnessRules());
        Reject(20, 10, new[] { point }, null);
        Reject(20, 10, new[] { point }, new FairnessRules(0));
        Reject(20, 10, new[] { point }, new FairnessRules(-1));
        Reject(20, 10, new[] { point }, new FairnessRules(11));
        Assert.True(JointAttackPlan.TryCreate(default, 0, 11, new[] { point }, new FairnessRules(11), out _));
    }

    [Fact]
    public void PreparationCostAndAttackTicks_RejectOverflowWithoutExceptions()
    {
        Assert.True(RuleTacticSelector.TryCreate(
            new TacticSettings(standardFormationPreparationTickCost: long.MaxValue), out var selector));
        Assert.False(selector!.Select(default).TryCreatePlan(out var invalid));
        Assert.Null(invalid);
        Assert.True(default(Tactic).TryCreatePlan(out var plan));
        Assert.False(plan!.TryCreateAttack(long.MaxValue, out var attack));
        Assert.Null(attack);
        Assert.False(plan.TryCreateAttack(long.MaxValue - plan.TelegraphTicks, out attack));
        Assert.Null(attack);
        Assert.True(plan.TryCreateAttack(long.MaxValue - plan.TelegraphTicks - plan.CounterWindowTicks, out attack));
        Assert.Equal(long.MaxValue, attack!.ExecutionTick);
        Assert.True(plan.TryCreateAttack(long.MinValue, out _));
    }

    [Fact]
    public void PlansSnapshotLayoutsAndAttacksHaveIndependentState()
    {
        var points = new[] { new ConnectionPoint(ConnectionPointKind.Signal, ConnectionPointPosition.Rear) };
        var original = points[0];
        Assert.True(JointAttackPlan.TryCreate(default, 0, 10, points, new FairnessRules(), out var plan));
        points[0] = new ConnectionPoint(ConnectionPointKind.Thread, ConnectionPointPosition.Front);
        Assert.Same(original, Assert.Single(plan!.ConnectionPoints));
        var list = Assert.IsAssignableFrom<IList<ConnectionPoint>>(plan.ConnectionPoints);
        Assert.Throws<NotSupportedException>(() => list[0] = points[0]);
        Assert.True(plan.TryCreateAttack(0, out var first));
        Assert.True(plan.TryCreateAttack(0, out var second));
        Assert.True(CounterResolver.Resolve(first, 0, InterferenceMethod.Disrupt, original).Success);
        Assert.Equal(JointAttackState.Telegraphing, second!.State);
        Assert.True(CounterResolver.Resolve(second, 9, InterferenceMethod.Disrupt, original).Success);
    }

    [Theory]
    [InlineData(ConnectionPointKind.Thread, InterferenceMethod.Cut)]
    [InlineData(ConnectionPointKind.Anchor, InterferenceMethod.Shoot)]
    [InlineData(ConnectionPointKind.Signal, InterferenceMethod.Disrupt)]
    public void CatalogMeaningIsFixedAcrossPositions(ConnectionPointKind kind, InterferenceMethod expected)
    {
        foreach (var position in Enum.GetValues<ConnectionPointPosition>())
        foreach (var method in new[] { InterferenceMethod.Cut, InterferenceMethod.Shoot, InterferenceMethod.Disrupt })
        {
            var point = new ConnectionPoint(kind, position);
            Assert.Equal(expected, point.AllowedMethods);
            Assert.True(JointAttackPlan.TryCreate(default, 20, 10, new[] { point }, new FairnessRules(), out var plan));
            Assert.True(plan!.TryCreateAttack(0, out var attack));
            Assert.Equal(method == expected, CounterResolver.Resolve(attack, 20, method, point).Success);
        }
    }

    private static void Reject(long telegraph, long window, IReadOnlyList<ConnectionPoint>? points, FairnessRules? rules)
    {
        Assert.False(JointAttackPlan.TryCreate(default, telegraph, window, points, rules, out var plan));
        Assert.Null(plan);
    }

    private static (ConnectionPointKind?, ConnectionPointPosition, InterferenceMethod) Signature(ConnectionPoint p)
        => (p.Kind, p.Position, p.AllowedMethods);

    private static Tactic[] Tactics() => new[] { default(BehaviorProfile), Profile(4, 0, 0),
        Profile(0, 4, 0), Profile(0, 0, 4) }.Select(p => new RuleTacticSelector().Select(p)).ToArray();

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
