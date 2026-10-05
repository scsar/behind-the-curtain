using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class DirectorObservationTests
{
    private static CombatObserver Observer()
    {
        Assert.True(CombatObserver.TryCreate(new ObserverSettings(), out var observer));
        return observer!;
    }
    private static AttackEvent Attack(int id, bool human = true, bool healer = false, bool area = false,
        AttackRange range = AttackRange.Melee, long tick = 0) =>
        new AttackEvent(area ? 2 : 0, healer ? TargetRole.Healer : TargetRole.Guard, range, tick, new AgentId(id), human);
    private static Expedition Expedition(double decay = 0.5)
    {
        Assert.True(Core.Expedition.TryCreate(new ExpeditionSettings(decay), new RuleTacticSelector(), out var expedition));
        return expedition!;
    }
    [Fact]
    public void BotsAreIgnoredAndUnknownHumansOnlyContributeToParty()
    {
        var observer = Observer();
        Assert.True(observer.Observe(Attack(1, range: AttackRange.Ranged)));
        Assert.True(observer.Observe(Attack(2, false, true, true, tick: 100)));
        Assert.True(observer.Observe(Attack(1, range: AttackRange.Ranged)));
        Assert.True(observer.Observe(Attack(1, range: AttackRange.Ranged)));
        Assert.Equal(1, observer.CurrentProfile.SustainedRangedRatio);
        Assert.Equal(0, observer.CurrentProfile.HealerFocus);
        Assert.Equal(0, observer.CurrentProfile.AreaDependency);
        Assert.Single(observer.CurrentIndividualProfiles);
        Assert.Equal(1, observer.CurrentIndividualProfiles[new AgentId(1)].SustainedRangedRatio);
        Assert.True(observer.Observe(new AttackEvent(2, TargetRole.Healer, AttackRange.Melee, 0)));
        Assert.Equal(0.25, observer.CurrentProfile.HealerFocus);
        Assert.Equal(0, observer.CurrentIndividualProfiles[new AgentId(1)].HealerFocus);
        Assert.False(observer.Observe(Attack(0)));
        observer.EndEncounterWithIndividuals();
        Assert.False(observer.Observe(Attack(2, false)));
    }
    [Fact]
    public void IndividualsHaveIndependentStreaksAndImmutableSnapshots()
    {
        var observer = Observer();
        for (int i = 0; i < 3; i++)
        {
            observer.Observe(Attack(1, healer: true, range: AttackRange.Ranged));
            observer.Observe(Attack(2, area: true));
        }
        var snapshot = observer.CurrentIndividualProfiles;
        Assert.Equal(1, snapshot[new AgentId(1)].HealerFocus);
        Assert.Equal(0, snapshot[new AgentId(1)].AreaDependency);
        Assert.Equal(1, snapshot[new AgentId(1)].SustainedRangedRatio);
        Assert.Equal(1, snapshot[new AgentId(2)].AreaDependency);
        Assert.Equal(0, snapshot[new AgentId(2)].HealerFocus);
        Assert.Equal(0, observer.CurrentProfile.SustainedRangedRatio);
        observer.Observe(Attack(1));
        var result = observer.EndEncounterWithIndividuals();
        Assert.Equal(0.75, result.IndividualProfiles[new AgentId(1)].HealerFocus);
        Assert.Equal(1, snapshot[new AgentId(1)].HealerFocus);
        Assert.Equal(result.PartyProfile.Values, observer.EndEncounter().Values);
    }
    [Theory]
    [InlineData(TacticId.StandardFormation, 1, 1, 1, 1)]
    [InlineData(TacticId.SpreadFormation, 1, 1, 1.75, 0.7)]
    [InlineData(TacticId.GuardHealer, 1, 1.75, 1, 1)]
    [InlineData(TacticId.ShieldAdvance, 1.75, 1, 1, 0.5)]
    public void BlendedIntentionsAreShiftLimitedAndRemainStable(TacticId expected, double approach, double intercept, double spread, double hold)
    {
        var expedition = Expedition();
        expedition.TryStartEncounter(out _);
        var observer = Observer();
        if (expected != TacticId.StandardFormation)
            for (int i = 0; i < 3; i++) observer.Observe(Attack(1,
                healer: expected == TacticId.GuardHealer, area: expected == TacticId.SpreadFormation,
                range: expected == TacticId.ShieldAdvance ? AttackRange.Ranged : AttackRange.Melee));
        expedition.EndEncounterWithIndividuals(observer.EndEncounterWithIndividuals());
        Assert.True(expedition.TryStartEncounter(out var tactic));
        Assert.Equal(expected, tactic.Id);
        var board = expedition.EncounterWorld!.GetBlackboard(Side.Puppets).Value;
        var next = Observer();
        next.Observe(Attack(9, healer: true, area: true));
        Assert.False(expedition.TryStartEncounter(out _));
        Assert.Equal(approach, board.GetIntention(ActionIds.Approach).Value);
        Assert.Equal(intercept, board.GetIntention(ActionIds.Intercept).Value);
        Assert.Equal(spread, board.GetIntention(ActionIds.Spread).Value);
        Assert.Equal(hold, board.GetIntention(ActionIds.Hold).Value);
        Assert.Equal(1, board.GetIntention(ActionIds.Attack).Value);
        expedition.EndEncounterWithIndividuals(next.EndEncounterWithIndividuals());
        Assert.Equal(intercept, board.GetIntention(ActionIds.Intercept).Value);
    }
    [Theory]
    [InlineData(1, 3)]
    [InlineData(3, 1)]
    public void ThreatsUseHighestMemoryThenLowestIdAndChangeOnlyAtStart(int first, int second)
    {
        var expedition = Expedition(0);
        expedition.TryStartEncounter(out _);
        var initial = expedition.EncounterWorld!.GetBlackboard(Side.Puppets).Value;
        Assert.False(initial.GetFact<AgentId>(TargetFactIds.HealerThreat).Success);
        var observer = Observer();
        foreach (var id in new[] { first, second }) observer.Observe(Attack(id, healer: true));
        for (int i = 0; i < 3; i++) observer.Observe(Attack(2, range: AttackRange.Ranged));
        observer.Observe(Attack(4));
        expedition.EndEncounterWithIndividuals(observer.EndEncounterWithIndividuals());
        Assert.False(initial.GetFact<AgentId>(TargetFactIds.HealerThreat).Success);
        expedition.TryStartEncounter(out _);
        var board = expedition.EncounterWorld!.GetBlackboard(Side.Puppets).Value;
        Assert.Equal(1, board.GetFact<AgentId>(TargetFactIds.HealerThreat).Value.Value);
        Assert.Equal(2, board.GetFact<AgentId>(TargetFactIds.RangedThreat).Value.Value);
        var next = Observer();
        next.Observe(Attack(4, healer: true));
        Assert.Equal(1, board.GetFact<AgentId>(TargetFactIds.HealerThreat).Value.Value);
        expedition.EndEncounterWithIndividuals(next.EndEncounterWithIndividuals());
        Assert.Equal(1, board.GetFact<AgentId>(TargetFactIds.HealerThreat).Value.Value);
        expedition.TryStartEncounter(out _);
        board = expedition.EncounterWorld!.GetBlackboard(Side.Puppets).Value;
        Assert.Equal(4, board.GetFact<AgentId>(TargetFactIds.HealerThreat).Value.Value);
        Assert.Equal(2, board.GetFact<AgentId>(TargetFactIds.RangedThreat).Value.Value);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IndividualMemoryDecaysIncludingAbsenceAndClearsAtExpeditionEnd(bool active)
    {
        var expedition = Expedition();
        expedition.TryStartEncounter(out _);
        var observer = Observer();
        observer.Observe(Attack(1, healer: true));
        observer.Observe(Attack(2, area: true));
        expedition.EndEncounterWithIndividuals(observer.EndEncounterWithIndividuals());
        expedition.TryStartEncounter(out _);
        observer = Observer();
        observer.Observe(Attack(1, area: true));
        var result = observer.EndEncounterWithIndividuals();
        Assert.True(expedition.EndEncounterWithIndividuals(result));
        Assert.False(expedition.EndEncounterWithIndividuals(result));
        Assert.Equal(0.8, expedition.IndividualMemory[new AgentId(1)].HealerFocus);
        Assert.Equal(0.5, expedition.IndividualMemory[new AgentId(1)].AreaDependency);
        Assert.Equal(0.9, expedition.IndividualMemory[new AgentId(2)].AreaDependency);
        if (active) expedition.TryStartEncounter(out _);
        expedition.EndExpedition();
        Assert.Empty(expedition.IndividualMemory);
        Assert.False(expedition.EndEncounterWithIndividuals(result));
        Assert.False(expedition.TryStartEncounter(out _));
        Assert.Empty(Expedition().IndividualMemory);
    }
    [Fact]
    public void ProfilesRetainValueEqualityIncludingDefaultZeros()
    {
        Assert.Equal(default(BehaviorProfile), Observer().CurrentProfile);
        var a = Observer();
        var b = Observer();
        a.Observe(Attack(1, healer: true));
        b.Observe(Attack(2, healer: true));
        Assert.Equal(a.CurrentProfile, b.CurrentProfile);
        Assert.Equal(a.CurrentProfile.GetHashCode(), b.CurrentProfile.GetHashCode());
        Assert.NotEqual(default(BehaviorProfile), a.CurrentProfile);
    }
    [Fact]
    public void InvalidIntentionSettingsAreRejectedAndCustomValuesAreUsed()
    {
        Assert.False(Core.Expedition.TryCreate(new ExpeditionSettings(intentions: new IntentionSettings(spread: double.NaN)),
            new RuleTacticSelector(), out _));
        Assert.True(Core.Expedition.TryCreate(new ExpeditionSettings(intentions: new IntentionSettings(spread: 4)),
            new RuleTacticSelector(), out var expedition));
        expedition!.TryStartEncounter(out _);
        var observer = Observer();
        observer.Observe(Attack(1, area: true));
        expedition.EndEncounterWithIndividuals(observer.EndEncounterWithIndividuals());
        expedition.TryStartEncounter(out _);
        Assert.Equal(1.75, expedition.EncounterWorld!.GetBlackboard(Side.Puppets).Value.GetIntention(ActionIds.Spread).Value);
    }
}
