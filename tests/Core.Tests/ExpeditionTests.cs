using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class ExpeditionTests
{
    [Fact]
    public void FirstEncounter_HasNoMemoryAndAlwaysUsesStandardFormation()
    {
        Assert.True(RuleTacticSelector.TryCreate(new TacticSettings(0, 0, 0), out var selector));
        Assert.True(Expedition.TryCreate(new ExpeditionSettings(), selector, out var expedition));
        Assert.NotNull(expedition);
        Assert.Null(expedition.Memory);
        Assert.True(expedition.TryStartEncounter(out var tactic));
        Assert.Equal(TacticId.StandardFormation, tactic.Id);
        Assert.Equal(TacticWeakness.None, tactic.Weakness);
        Assert.Equal(0, tactic.PreparationTickCost);
        Assert.True(expedition.EndEncounter(Profile(4, 2, 1)));
        AssertProfile(expedition, 1, 0.5, 0.25);
    }

    [Fact]
    public void SecondAndThirdEncounters_MergeEveryTendencyAndSelectFromMemory()
    {
        var expedition = Create();
        Complete(expedition, Profile(4, 0, 2));
        Assert.True(expedition.TryStartEncounter(out var second));
        Assert.Equal(TacticId.SpreadFormation, second.Id);
        Assert.True(expedition.EndEncounter(Profile(0, 4, 4)));
        AssertProfile(expedition, 0.8, 0.5, 0.75);

        // The latest profile alone would choose GuardHealer (tied margins, healer priority).
        // Slowly falling area suspicion keeps SpreadFormation above ranged memory.
        Assert.True(expedition.TryStartEncounter(out var third));
        Assert.Equal(TacticId.SpreadFormation, third.Id);
        Assert.True(expedition.EndEncounter(Profile(2, 4, 0)));
        AssertProfile(expedition, 0.74, 0.75, 0.6);
        Assert.True(expedition.TryStartEncounter(out var fourth));
        Assert.Equal(TacticId.GuardHealer, fourth.Id);
    }

    [Theory]
    [InlineData(0, 0.8, 1, 0.7, 0.74, 0.8, 1)]
    [InlineData(0.99, 0.8, 0.01, 0.7, 0.74, 0.008, 0.703)]
    public void DecayEndpoints_UseInjectedWeight(double decay, double area2, double healer2,
        double ranged2, double area3, double healer3, double ranged3)
    {
        var expedition = Create(decay);
        Complete(expedition, Profile(4, 0, 3));
        AssertProfile(expedition, 1, 0, 0.75);
        Complete(expedition, Profile(0, 4, 2));
        AssertProfile(expedition, area2, healer2, ranged2);
        Complete(expedition, Profile(2, 0, 4));
        AssertProfile(expedition, area3, healer3, ranged3);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidDecay_RejectsCreation(double decay)
    {
        Assert.False(Expedition.TryCreate(new ExpeditionSettings(decay), new RuleTacticSelector(), out var expedition));
        Assert.Null(expedition);
    }

    [Fact]
    public void NullDependencies_RejectCreation()
    {
        Assert.False(Expedition.TryCreate(null, new RuleTacticSelector(), out var missingSettings));
        Assert.Null(missingSettings);
        Assert.False(Expedition.TryCreate(new ExpeditionSettings(), null, out var missingSelector));
        Assert.Null(missingSelector);
    }

    [Fact]
    public void InvalidEncounterTransitions_DoNotMergeTwice()
    {
        var expedition = Create();
        Assert.False(expedition.EndEncounter(Profile(4, 4, 4)));
        Assert.Null(expedition.Memory);
        Assert.True(expedition.TryStartEncounter(out _));
        Assert.False(expedition.TryStartEncounter(out _));
        Assert.True(expedition.EndEncounter(Profile(4, 0, 0)));
        Assert.False(expedition.EndEncounter(default));
        AssertProfile(expedition, 1, 0, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EndExpedition_ClearsMemoryAndNewExpeditionDoesNotInheritIt(bool activeEncounter)
    {
        var selector = new RuleTacticSelector();
        Assert.True(Expedition.TryCreate(new ExpeditionSettings(), selector, out var old));
        Assert.NotNull(old);
        Complete(old, Profile(4, 4, 4));
        if (activeEncounter)
            Assert.True(old.TryStartEncounter(out _));
        old.EndExpedition();
        old.EndExpedition();
        Assert.Null(old.Memory);
        Assert.False(old.EndEncounter(Profile(4, 4, 4)));
        Assert.False(old.TryStartEncounter(out _));
        Assert.Null(old.Memory);

        Assert.True(Expedition.TryCreate(new ExpeditionSettings(), selector, out var next));
        Assert.NotNull(next);
        Assert.Null(next.Memory);
        Assert.True(next.TryStartEncounter(out var tactic));
        Assert.Equal(TacticId.StandardFormation, tactic.Id);
        Assert.True(next.EndEncounter(Profile(0, 2, 0)));
        AssertProfile(next, 0, 0.5, 0);
    }

    [Fact]
    public void EmptyFirstProfile_IsRememberedAndIncludedInNextAverage()
    {
        var expedition = Create();
        Complete(expedition, default);
        AssertProfile(expedition, 0, 0, 0);
        Complete(expedition, Profile(4, 4, 4));
        AssertProfile(expedition, 0.5, 0.5, 0.5);
    }

    private static Expedition Create(double decay = ExpeditionSettings.DefaultDecay)
    {
        Assert.Equal(0.5, new ExpeditionSettings().Decay);
        Assert.True(Expedition.TryCreate(new ExpeditionSettings(decay), new RuleTacticSelector(), out var expedition));
        Assert.NotNull(expedition);
        return expedition;
    }

    private static void Complete(Expedition expedition, BehaviorProfile profile)
    {
        Assert.True(expedition.TryStartEncounter(out _));
        Assert.True(expedition.EndEncounter(profile));
    }

    private static void AssertProfile(Expedition expedition, double area, double healer, double ranged)
    {
        Assert.True(expedition.Memory.HasValue);
        var memory = expedition.Memory.Value;
        Assert.Equal(area, memory.AreaDependency, 12);
        Assert.Equal(healer, memory.HealerFocus, 12);
        Assert.Equal(ranged, memory.SustainedRangedRatio, 12);
        Assert.InRange(memory.AreaDependency, 0, 1);
        Assert.InRange(memory.HealerFocus, 0, 1);
        Assert.InRange(memory.SustainedRangedRatio, 0, 1);
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
