using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class PlayerAttackTests
{
    private static readonly AttackTarget[] Targets = {
        new(3, 0, TargetRole.Guard), new(2, 21, TargetRole.Attacker),
        new(2, 20, TargetRole.Healer), new(4, 0, TargetRole.Guard)
    };

    [Theory]
    [InlineData(PlayerAttackKind.AreaMelee, new[] { 0, 1, 2 }, 1, 0)]
    [InlineData(PlayerAttackKind.Melee, new[] { 1 }, 0, 0)]
    [InlineData(PlayerAttackKind.Ranged, new[] { 2 }, 0, 1)]
    public void SelectsWithinInclusiveBoundsAndRecordsOnce(PlayerAttackKind kind, int[] hits, double area, double healer)
    {
        Assert.True(CombatObserver.TryCreate(new ObserverSettings(), out var observer));
        Assert.True(PlayerAttack.TryResolve(kind, Targets, 3, 20, 0, observer!, out var actual));
        Assert.Equal(hits, actual);
        Assert.Equal(area, observer!.CurrentProfile.AreaDependency);
        Assert.Equal(healer, observer.CurrentProfile.HealerFocus);
        Assert.True(observer.Observe(new AttackEvent(0, TargetRole.Guard, AttackRange.Melee, 1)));
        Assert.Equal(area / 2, observer.CurrentProfile.AreaDependency);
        Assert.Equal(healer / 2, observer.CurrentProfile.HealerFocus);
    }

    [Fact]
    public void MissDoesNotRecordOrBreakRangedStreak_AndClosedObserverRejectsHits()
    {
        Assert.True(CombatObserver.TryCreate(new ObserverSettings(), out var observer));
        for (var i = 0; i < 3; i++)
        {
            Assert.True(PlayerAttack.TryResolve(PlayerAttackKind.Ranged, Targets, 3, 20, i, observer!, out _));
            Assert.False(PlayerAttack.TryResolve(PlayerAttackKind.Melee, Targets, 1, 20, i, observer!, out var missed));
            Assert.Empty(missed);
        }
        Assert.Equal(1, observer!.CurrentProfile.SustainedRangedRatio);
        Assert.Equal(1, observer.CurrentProfile.HealerFocus);
        observer.EndEncounter();
        Assert.False(PlayerAttack.TryResolve(PlayerAttackKind.Melee, Targets, 3, 20, 4, observer, out var hits));
        Assert.Empty(hits);
    }

    [Theory]
    [InlineData(double.NaN, 20)]
    [InlineData(double.PositiveInfinity, 20)]
    [InlineData(0, 20)]
    [InlineData(3, -1)]
    [InlineData(3, 181)]
    public void InvalidSettingsDoNotObserve(double radius, double cone)
    {
        CombatObserver.TryCreate(new ObserverSettings(), out var observer);
        Assert.False(PlayerAttack.TryResolve(PlayerAttackKind.AreaMelee, Targets, radius, cone, 0, observer!, out var hits));
        Assert.Empty(hits);
        Assert.Equal(default, observer!.CurrentProfile);
    }
}
