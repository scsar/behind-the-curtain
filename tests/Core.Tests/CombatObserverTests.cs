using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class CombatObserverTests
{
    [Fact]
    public void CurrentProfile_IsAnImmutableNonDestructiveSnapshot()
    {
        var observer = Create();
        AssertProfile(observer.CurrentProfile, 0, 0, 0);
        for (var i = 0; i < 2; i++)
            Assert.True(observer.Observe(new AttackEvent(2, TargetRole.Healer, AttackRange.Ranged, i)));
        var snapshot = observer.CurrentProfile;
        AssertProfile(snapshot, 1, 1, 0);
        AssertProfile(observer.CurrentProfile, 1, 1, 0);
        Assert.True(observer.Observe(new AttackEvent(0, TargetRole.Guard, AttackRange.Ranged, 2)));
        AssertProfile(observer.CurrentProfile, 2d / 3, 2d / 3, 1);
        AssertProfile(snapshot, 1, 1, 0);
        Assert.False(observer.Observe(new AttackEvent(-1, TargetRole.Guard, AttackRange.Melee, 3)));
        AssertProfile(observer.CurrentProfile, 2d / 3, 2d / 3, 1);
        AssertProfile(observer.EndEncounter(), 2d / 3, 2d / 3, 1);
        Assert.False(observer.Observe(new AttackEvent(0, TargetRole.Guard, AttackRange.Melee, 3)));
        AssertProfile(observer.CurrentProfile, 2d / 3, 2d / 3, 1);
    }

    [Fact]
    public void EmptyEncounter_ReturnsZeros()
    {
        AssertProfile(Create().EndEncounter(), 0, 0, 0);
    }

    [Fact]
    public void AreaDependency_CountsAttacksMeetingThresholdRatherThanEnemies()
    {
        var observer = Create();
        foreach (var hits in new[] { 0, 1, 2, 50 })
            Assert.True(observer.Observe(new AttackEvent(hits, TargetRole.Guard, AttackRange.Melee, 0)));
        AssertProfile(observer.EndEncounter(), 0.5, 0, 0);
    }

    [Fact]
    public void HealerFocus_CountsOnlyPrimaryHealerTargets()
    {
        var observer = Create();
        foreach (var role in new[] { TargetRole.Healer, TargetRole.Guard, TargetRole.Attacker, TargetRole.Healer })
            Assert.True(observer.Observe(new AttackEvent(0, role, AttackRange.Melee, 0)));
        AssertProfile(observer.EndEncounter(), 0, 0.5, 0);
    }

    [Theory]
    [InlineData("RR", 0)]
    [InlineData("RRR", 1)]
    [InlineData("RRRR", 1)]
    [InlineData("RRMRR", 0)]
    [InlineData("RRRMRR", 0.5)]
    [InlineData("RRRMRRRRMRR", 7.0 / 11)]
    [InlineData("MRRR", 0.75)]
    [InlineData("RRRM", 0.75)]
    public void SustainedRangedRatio_CountsWholeQualifyingRuns(string attacks, double expected)
    {
        var observer = Create();
        Feed(observer, attacks);
        AssertProfile(observer.EndEncounter(), 0, 0, expected);
    }

    [Theory]
    [InlineData(1, 1, 0.75, 0.75)]
    [InlineData(2, 3, 0.5, 0.75)]
    [InlineData(3, 4, 0.25, 0)]
    [InlineData(int.MaxValue, int.MaxValue, 0, 0)]
    public void Settings_ChangeBothThresholds(int minimumHits, int streak, double area, double ranged)
    {
        var observer = Create(new ObserverSettings(minimumHits, streak));
        for (var i = 0; i < 4; i++)
            Assert.True(observer.Observe(new AttackEvent(i, TargetRole.Healer,
                i == 0 ? AttackRange.Melee : AttackRange.Ranged, i)));
        AssertProfile(observer.EndEncounter(), area, 1, ranged);
    }

    [Fact]
    public void DefaultSettings_UseProvisionalDesignValues()
    {
        var settings = new ObserverSettings();
        Assert.Equal(2, settings.MinimumAreaHitCount);
        Assert.Equal(3, settings.SustainedRangedStreak);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(-1, 3)]
    [InlineData(2, 0)]
    [InlineData(2, -1)]
    public void InvalidSettings_AreRejected(int minimumHits, int streak)
    {
        Assert.False(CombatObserver.TryCreate(new ObserverSettings(minimumHits, streak), out var observer));
        Assert.Null(observer);
    }

    [Fact]
    public void NullSettings_AreRejected()
    {
        Assert.False(CombatObserver.TryCreate(null, out var observer));
        Assert.Null(observer);
    }

    [Fact]
    public void InvalidEvents_AreRejectedWithoutCountingOrBreakingStreak()
    {
        var observer = Create();
        Assert.True(observer.Observe(new AttackEvent(2, TargetRole.Healer, AttackRange.Ranged, 10)));
        Assert.False(observer.Observe(new AttackEvent(-1, TargetRole.Guard, AttackRange.Melee, 20)));
        Assert.False(observer.Observe(new AttackEvent(0, (TargetRole)99, AttackRange.Melee, 20)));
        Assert.False(observer.Observe(new AttackEvent(0, TargetRole.Guard, (AttackRange)99, 20)));
        Assert.False(observer.Observe(new AttackEvent(0, TargetRole.Guard, AttackRange.Melee, 9)));
        Assert.True(observer.Observe(new AttackEvent(2, TargetRole.Healer, AttackRange.Ranged, 10)));
        Assert.True(observer.Observe(new AttackEvent(2, TargetRole.Healer, AttackRange.Ranged, 11)));
        AssertProfile(observer.EndEncounter(), 1, 1, 1);
    }

    [Fact]
    public void SignedTicksAndTimeGaps_DoNotBreakStreak()
    {
        var observer = Create();
        foreach (var tick in new[] { long.MinValue, 0, long.MaxValue })
            Assert.True(observer.Observe(new AttackEvent(0, TargetRole.Guard, AttackRange.Ranged, tick)));
        AssertProfile(observer.EndEncounter(), 0, 0, 1);
    }

    [Fact]
    public void EndEncounter_IsStableAndRejectsLaterEvents()
    {
        var observer = Create();
        Feed(observer, "RRR");
        var profile = observer.EndEncounter();
        Assert.False(observer.Observe(new AttackEvent(2, TargetRole.Healer, AttackRange.Melee, 100)));
        AssertProfile(profile, 0, 0, 1);
        AssertProfile(observer.EndEncounter(), 0, 0, 1);
    }

    [Fact]
    public void AllShortAttackSequences_StayWithinUnitInterval()
    {
        // Exhaust all melee/ranged sequences up to eight attacks at several thresholds.
        for (var threshold = 1; threshold <= 4; threshold++)
        for (var length = 0; length <= 8; length++)
        for (var mask = 0; mask < (1 << length); mask++)
        {
            var observer = Create(new ObserverSettings(threshold, threshold));
            for (var i = 0; i < length; i++)
                Assert.True(observer.Observe(new AttackEvent(i % 5, (TargetRole)(i % 3),
                    (mask & (1 << i)) == 0 ? AttackRange.Melee : AttackRange.Ranged, i)));
            var profile = observer.EndEncounter();
            Assert.InRange(profile.AreaDependency, 0, 1);
            Assert.InRange(profile.HealerFocus, 0, 1);
            Assert.InRange(profile.SustainedRangedRatio, 0, 1);
        }
    }

    private static void Feed(CombatObserver observer, string attacks)
    {
        for (var i = 0; i < attacks.Length; i++)
            Assert.True(observer.Observe(new AttackEvent(0, TargetRole.Guard,
                attacks[i] == 'R' ? AttackRange.Ranged : AttackRange.Melee, i)));
    }

    private static CombatObserver Create(ObserverSettings? settings = null)
    {
        Assert.True(CombatObserver.TryCreate(settings ?? new ObserverSettings(), out var observer));
        Assert.NotNull(observer);
        return observer;
    }

    private static void AssertProfile(BehaviorProfile profile, double area, double healer, double ranged)
    {
        Assert.Equal(area, profile.AreaDependency, 12);
        Assert.Equal(healer, profile.HealerFocus, 12);
        Assert.Equal(ranged, profile.SustainedRangedRatio, 12);
    }
}
