using System;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class RuleTacticSelectorTests
{
    [Theory]
    [InlineData(4, 0, 0, TacticId.SpreadFormation, TacticWeakness.IsolatedPuppets, 10)]
    [InlineData(0, 4, 0, TacticId.GuardHealer, TacticWeakness.ExposedConnectionPoint, 5)]
    [InlineData(0, 0, 4, TacticId.ShieldAdvance, TacticWeakness.OpenFlank, 15)]
    [InlineData(1, 1, 1, TacticId.StandardFormation, TacticWeakness.None, 0)]
    [InlineData(0, 0, 0, TacticId.StandardFormation, TacticWeakness.None, 0)]
    public void Select_ReturnsTacticData(int area, int healer, int ranged,
        TacticId id, TacticWeakness weakness, long cost)
    {
        ITacticSelector selector = new RuleTacticSelector();
        var result = selector.Select(Profile(area, healer, ranged));
        Assert.Equal(id, result.Id);
        Assert.Equal(weakness, result.Weakness);
        Assert.Equal(cost, result.PreparationTickCost);
    }

    [Theory]
    [InlineData(2, 0, 0, TacticId.SpreadFormation)]
    [InlineData(0, 2, 0, TacticId.GuardHealer)]
    [InlineData(0, 0, 2, TacticId.ShieldAdvance)]
    public void Threshold_IsInclusiveAndRejectsImmediatelyLowerValue(
        int area, int healer, int ranged, TacticId expected)
    {
        var profile = Profile(area, healer, ranged);
        Assert.Equal(expected, new RuleTacticSelector().Select(profile).Id);
        // 0.5 is the representable double immediately below this threshold.
        var threshold = Math.BitIncrement(0.5);
        var selector = Create(new TacticSettings(threshold, threshold, threshold));
        Assert.Equal(TacticId.StandardFormation, selector.Select(profile).Id);
    }

    [Theory]
    [InlineData(4, 3, 2, TacticId.SpreadFormation)]
    [InlineData(2, 4, 3, TacticId.GuardHealer)]
    [InlineData(3, 2, 4, TacticId.ShieldAdvance)]
    [InlineData(4, 4, 4, TacticId.GuardHealer)]
    [InlineData(4, 4, 0, TacticId.GuardHealer)]
    [InlineData(0, 4, 4, TacticId.GuardHealer)]
    [InlineData(4, 0, 4, TacticId.SpreadFormation)]
    [InlineData(2, 2, 2, TacticId.GuardHealer)]
    public void MultipleCandidates_UseMarginThenPriority(int area, int healer, int ranged, TacticId expected)
    {
        Assert.Equal(expected, new RuleTacticSelector().Select(Profile(area, healer, ranged)).Id);
    }

    [Fact]
    public void DifferentThresholds_CompareMarginsRatherThanRawTendencies()
    {
        var selector = Create(new TacticSettings(0.75, 0.25, 0.5));
        Assert.Equal(TacticId.GuardHealer, selector.Select(Profile(4, 3, 3)).Id);
        // Different raw tendencies with identical margins retain priority.
        Assert.Equal(TacticId.GuardHealer, selector.Select(Profile(4, 2, 3)).Id);
    }

    [Fact]
    public void ChangedThresholdsAndCosts_AreUsedForEveryTactic()
    {
        var selector = Create(new TacticSettings(0.25, 0.25, 0.25, 21, 22, 23, 24));
        var profiles = new[] { Profile(1, 0, 0), Profile(0, 1, 0), Profile(0, 0, 1), default };
        var ids = new[] { TacticId.SpreadFormation, TacticId.GuardHealer,
            TacticId.ShieldAdvance, TacticId.StandardFormation };
        for (var i = 0; i < profiles.Length; i++)
        {
            var result = selector.Select(profiles[i]);
            Assert.Equal(ids[i], result.Id);
            Assert.Equal(21L + i, result.PreparationTickCost);
        }
    }

    [Fact]
    public void ThresholdEndpoints_AreValid()
    {
        Assert.Equal(TacticId.GuardHealer, Create(new TacticSettings(0, 0, 0)).Select(default).Id);
        var selector = Create(new TacticSettings(1, 1, 1));
        Assert.Equal(TacticId.StandardFormation, selector.Select(Profile(3, 3, 3)).Id);
        Assert.Equal(TacticId.GuardHealer, selector.Select(Profile(4, 4, 4)).Id);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidThresholds_AreRejected(double value)
    {
        AssertRejected(new TacticSettings(areaDependencyThreshold: value));
        AssertRejected(new TacticSettings(healerFocusThreshold: value));
        AssertRejected(new TacticSettings(sustainedRangedThreshold: value));
    }

    [Fact]
    public void NullSettingsAndNegativeCosts_AreRejected()
    {
        AssertRejected(null);
        AssertRejected(new TacticSettings(spreadFormationPreparationTickCost: -1));
        AssertRejected(new TacticSettings(guardHealerPreparationTickCost: -1));
        AssertRejected(new TacticSettings(shieldAdvancePreparationTickCost: -1));
        AssertRejected(new TacticSettings(standardFormationPreparationTickCost: -1));
    }

    private static void AssertRejected(TacticSettings? settings)
    {
        Assert.False(RuleTacticSelector.TryCreate(settings, out var selector));
        Assert.Null(selector);
    }

    private static RuleTacticSelector Create(TacticSettings settings)
    {
        Assert.True(RuleTacticSelector.TryCreate(settings, out var selector));
        Assert.NotNull(selector);
        return selector;
    }

    private static BehaviorProfile Profile(int area, int healer, int ranged)
    {
        Assert.True(CombatObserver.TryCreate(new ObserverSettings(sustainedRangedStreak: 1), out var observer));
        Assert.NotNull(observer);
        for (var i = 0; i < 4; i++)
            Assert.True(observer.Observe(new AttackEvent(i < area ? 2 : 0,
                i < healer ? TargetRole.Healer : TargetRole.Guard,
                i < ranged ? AttackRange.Ranged : AttackRange.Melee, i)));
        return observer.EndEncounter();
    }
}
