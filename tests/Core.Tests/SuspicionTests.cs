using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class SuspicionTests
{
    [Fact]
    public void ChangedHabitFallsSlowlyAndAbsenceFallsEvenSlower_ThreatSurvivesNextEncounter()
    {
        var expedition = Create();
        Complete(expedition, Observe(1, TargetRole.Healer));
        Complete(expedition, Observe(1, TargetRole.Guard));
        Assert.Equal(0.8, expedition.Memory!.Value.HealerFocus, 12);
        Assert.Equal(0.8, expedition.IndividualMemory[new AgentId(1)].HealerFocus, 12);
        Assert.True(expedition.TryStartEncounter(out var tactic));
        Assert.Equal(TacticId.GuardHealer, tactic.Id);
        Assert.Equal(new AgentId(1), expedition.EncounterWorld!.GetBlackboard(Side.Puppets).Value
            .GetFact<AgentId>(TargetFactIds.HealerThreat).Value);
        Assert.True(CombatObserver.TryCreate(new ObserverSettings(), out var empty));
        Assert.True(expedition.EndEncounterWithIndividuals(empty!.EndEncounterWithIndividuals()));
        Assert.Equal(0.72, expedition.Memory!.Value.HealerFocus, 12);
        Assert.Equal(0.72, expedition.IndividualMemory[new AgentId(1)].HealerFocus, 12);
        Assert.True(expedition.TryStartEncounter(out tactic));
        Assert.Equal(TacticId.GuardHealer, tactic.Id);
        Assert.Equal(new AgentId(1), expedition.EncounterWorld!.GetBlackboard(Side.Puppets).Value
            .GetFact<AgentId>(TargetFactIds.HealerThreat).Value);
    }

    [Fact]
    public void PartyAttacksDoNotCountAsAbsentWhenAllTendenciesAreZeroOrAttackerIsUnknown()
    {
        var expedition = Create();
        Complete(expedition, Observe(1, TargetRole.Healer));
        Assert.True(CombatObserver.TryCreate(new ObserverSettings(), out var observer));
        observer!.Observe(new AttackEvent(0, TargetRole.Guard, AttackRange.Melee, 0));
        Assert.True(observer.CurrentProfile.HasAttacks);
        // Keep the legacy profile-only entry point working, including unknown attackers.
        Assert.True(expedition.TryStartEncounter(out _));
        Assert.True(expedition.EndEncounter(observer.EndEncounter()));
        Assert.Equal(0.8, expedition.Memory!.Value.HealerFocus, 12);
        Assert.Equal(0.9, expedition.IndividualMemory[new AgentId(1)].HealerFocus, 12);
        Assert.Equal(default(BehaviorProfile), observer.CurrentProfile); // equality still compares tendencies
        Assert.True(expedition.TryStartEncounter(out _));
        Assert.True(expedition.EndEncounter(default));
        Assert.Equal(0.72, expedition.Memory!.Value.HealerFocus, 12);
    }

    [Fact]
    public void EachTendencyUsesItsOwnDirectionAndCustomWeights()
    {
        var expedition = Create(new ExpeditionSettings(riseWeight: 0.25, fallWeight: 0.4, absentFallWeight: 0.05));
        Complete(expedition, Observe(1, TargetRole.Healer));
        Complete(expedition, Observe(1, TargetRole.Guard, area: 2));
        Assert.Equal(0.25, expedition.Memory!.Value.AreaDependency, 12);
        Assert.Equal(0.6, expedition.Memory!.Value.HealerFocus, 12);
        Assert.Equal(0.25, expedition.IndividualMemory[new AgentId(1)].AreaDependency, 12);
        Assert.Equal(0.6, expedition.IndividualMemory[new AgentId(1)].HealerFocus, 12);
        Complete(expedition, default);
        Assert.Equal(0.2375, expedition.Memory!.Value.AreaDependency, 12);
        Assert.Equal(0.57, expedition.Memory!.Value.HealerFocus, 12);
        Assert.Equal(0.75, new ExpeditionSettings(riseWeight: 0.25).Decay);
        Assert.Equal(0.8, new ExpeditionSettings(decay: 0.2).RiseWeight);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidWeightsAreRejected(double value)
    {
        foreach (var settings in new[] { new ExpeditionSettings(riseWeight: value),
            new ExpeditionSettings(fallWeight: value), new ExpeditionSettings(absentFallWeight: value) })
            Assert.False(Expedition.TryCreate(settings, new RuleTacticSelector(), out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ExplicitWeightsAllowBothEndpoints(double weight)
    {
        var expedition = Create(new ExpeditionSettings(riseWeight: weight, fallWeight: weight, absentFallWeight: weight));
        Complete(expedition, Observe(1, TargetRole.Healer));
        Complete(expedition, Observe(1, TargetRole.Guard, area: 2));
        Assert.Equal(1 - weight, expedition.Memory!.Value.HealerFocus);
        Assert.Equal(weight, expedition.Memory!.Value.AreaDependency);
        Complete(expedition, default);
        Assert.Equal(weight * (1 - weight), expedition.Memory!.Value.AreaDependency);
    }

    [Fact]
    public void BotOnlyEncounterIsAbsentForPartyAndIndividuals()
    {
        var expedition = Create();
        Complete(expedition, Observe(1, TargetRole.Healer));
        Assert.True(CombatObserver.TryCreate(new ObserverSettings(), out var observer));
        observer!.Observe(new AttackEvent(0, TargetRole.Guard, AttackRange.Melee, 0, new AgentId(1), false));
        Complete(expedition, observer.EndEncounterWithIndividuals());
        Assert.Equal(0.9, expedition.Memory!.Value.HealerFocus, 12);
        Assert.Equal(0.9, expedition.IndividualMemory[new AgentId(1)].HealerFocus, 12);
    }

    internal static Expedition Create(ExpeditionSettings? settings = null)
    {
        Assert.True(Expedition.TryCreate(settings ?? new ExpeditionSettings(), new RuleTacticSelector(), out var result));
        return result!;
    }
    internal static EncounterObservation Observe(int id, TargetRole role, int area = 0, AttackRange range = AttackRange.Melee)
    {
        Assert.True(CombatObserver.TryCreate(new ObserverSettings(), out var observer));
        for (int tick = 0; tick < 3; tick++)
            Assert.True(observer!.Observe(new AttackEvent(area, role, range, tick, new AgentId(id), true)));
        return observer!.EndEncounterWithIndividuals();
    }
    internal static void Complete(Expedition expedition, EncounterObservation observation)
    {
        Assert.True(expedition.TryStartEncounter(out _));
        Assert.True(expedition.EndEncounterWithIndividuals(observation));
    }
}
