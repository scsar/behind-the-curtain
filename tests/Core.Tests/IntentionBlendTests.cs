using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class IntentionBlendTests
{
    private static EncounterObservation Observation(int healer, int ranged, int total = 10, int area = 0)
    {
        CombatObserver.TryCreate(new ObserverSettings(), out var observer);
        for (int i = 0; i < total; i++)
            Assert.True(observer!.Observe(new AttackEvent(i < area ? 2 : 0,
                i < healer ? TargetRole.Healer : TargetRole.Guard,
                i < ranged ? AttackRange.Ranged : AttackRange.Melee, i, new AgentId(1), true)));
        return observer!.EndEncounterWithIndividuals();
    }
    [Theory]
    [InlineData(4999, 0.5, 0)] [InlineData(5000, 0.5, 0.5)]
    [InlineData(10000, 0.5, 1)] [InlineData(10000, 1, 1)]
    public void StrengthBoundaries(int healer, double threshold, double expected)
    {
        Assert.True(RuleTacticSelector.TryCreate(new TacticSettings(healerFocusThreshold: threshold), out var selector));
        Assert.Equal(expected, selector!.GetStrengths(Observation(healer, 0, 10000).PartyProfile)[TacticId.GuardHealer], 12);
    }
    [Fact]
    public void AllQualifyingTendenciesBlendAndMissingKeysAreNeutral()
    {
        var selector = new RuleTacticSelector();
        var profile = Observation(8, 9).PartyProfile;
        Assert.Equal(TacticId.ShieldAdvance, selector.Select(profile).Id);
        var intentions = new IntentionSettings();
        var target = intentions.Blend(selector.GetStrengths(profile));
        Assert.Equal(2.2, target[ActionIds.Intercept], 12);
        Assert.Equal(1.9, target[ActionIds.Approach], 12);
        Assert.Equal(0.55, target[ActionIds.Hold], 12);
        var single = intentions.Blend(selector.GetStrengths(Observation(5, 0).PartyProfile));
        Assert.Equal(1.75, single[ActionIds.Intercept], 12);
        Assert.Equal(1, single[ActionIds.Approach]);
        Assert.All(intentions.Blend(selector.GetStrengths(default)).Values, x => Assert.Equal(1, x));
        var custom = new IntentionSettings(overrides: new Dictionary<TacticId, IReadOnlyDictionary<string, double>> {
            [TacticId.GuardHealer] = new Dictionary<string, double> { ["Guard:attack"] = 3 },
            [TacticId.SpreadFormation] = new Dictionary<string, double> { [ActionIds.Hold] = 0 },
            [TacticId.ShieldAdvance] = new Dictionary<string, double> { [ActionIds.Hold] = 0 } });
        var blended = custom.Blend(selector.GetStrengths(Observation(10, 10, area: 10).PartyProfile));
        Assert.Equal(3, blended["Guard:attack"]);
        Assert.Equal(0, blended[ActionIds.Hold]);
    }
    [Fact]
    public void ShiftIsBoundedInBothDirectionsAndRejectedStartsDoNotShift()
    {
        var expedition = SuspicionTests.Create(new ExpeditionSettings(riseWeight: 1, fallWeight: 1));
        SuspicionTests.Complete(expedition, Observation(8, 9));
        expedition.TryStartEncounter(out _);
        Assert.Equal(1.75, expedition.PreviousIntentions[ActionIds.Intercept]);
        Assert.False(expedition.TryStartEncounter(out _));
        Assert.Equal(1.75, expedition.PreviousIntentions[ActionIds.Intercept]);
        expedition.EndEncounterWithIndividuals(Observation(8, 9));
        expedition.TryStartEncounter(out _);
        Assert.Equal(2.2, expedition.PreviousIntentions[ActionIds.Intercept], 12);
        expedition.EndEncounterWithIndividuals(Observation(0, 0));
        expedition.TryStartEncounter(out _);
        Assert.Equal(1.45, expedition.PreviousIntentions[ActionIds.Intercept], 12);
        expedition.EndEncounterWithIndividuals(Observation(0, 0));
        expedition.TryStartEncounter(out _);
        Assert.Equal(1, expedition.PreviousIntentions[ActionIds.Intercept]);
    }
    [Theory]
    [InlineData(8, 0.4, true)] [InlineData(6, 0.3, false)]
    public void CarryOverContainsOnlyAttenuatedIndividuals(int healer, double carried, bool threat)
    {
        var old = SuspicionTests.Create();
        SuspicionTests.Complete(old, Observation(healer, 9));
        var snapshot = old.CreateCarryOverSnapshot();
        old.EndExpedition();
        Assert.Equal(carried, snapshot.IndividualMemory[new AgentId(1)].HealerFocus, 12);
        Assert.True(Expedition.TryCreate(new ExpeditionSettings(), new RuleTacticSelector(), snapshot, out var fresh));
        Assert.Null(fresh!.Memory);
        Assert.All(fresh.PreviousIntentions.Values, x => Assert.Equal(1, x));
        fresh.TryStartEncounter(out var tactic);
        Assert.Equal(TacticId.StandardFormation, tactic.Id);
        var board = fresh.EncounterWorld!.GetBlackboard(Side.Puppets).Value;
        Assert.Equal(threat, board.GetFact<AgentId>(TargetFactIds.HealerThreat).Success);
        Assert.Equal(threat, board.GetFact<double>(TargetFactIds.HealerThreatStrength).Success);
        if (threat) Assert.Equal(carried, board.GetFact<double>(TargetFactIds.HealerThreatStrength).Value, 12);
    }
    private static void Add(World world, int id, TargetRole role, Vec2 position, Side side = Side.Puppets)
        => Assert.True(world.Add(new AgentState(new AgentId(id), side, side == Side.Players,
            role, position, new Vec2(0, 1), 0.3)).Success);
    [Theory]
    [InlineData(TacticId.GuardHealer, true, 0.8, 1)]
    [InlineData(TacticId.ShieldAdvance, true, 0.8, 0.4)]
    [InlineData(TacticId.StandardFormation, true, 0.01, 0.02)]
    [InlineData(TacticId.StandardFormation, false, 0.8, 0)]
    public void IndependentInterceptSuitability(TacticId tactic, bool threat, double strength, double expected)
    {
        var world = new World();
        Add(world, 1, TargetRole.Attacker, new Vec2(8, 0), Side.Players);
        Add(world, 10, TargetRole.Guard, new Vec2(0, 0));
        Add(world, 13, TargetRole.Healer, new Vec2(-3, 0));
        var board = world.GetBlackboard(Side.Puppets).Value;
        board.SetFact("tactic", tactic);
        if (threat) { board.SetFact(TargetFactIds.HealerThreat, new AgentId(1)); board.SetFact(TargetFactIds.HealerThreatStrength, strength); }
        var brain = PuppetBrain.Create(world, new PuppetSettings(), new DeterministicRandom(1)).Value;
        brain.Tick(0);
        var score = brain.GetDecision(new AgentId(10)).Value.Selection.Scores.Single(s => s.ActionId == ActionIds.Intercept);
        Assert.Equal(expected, score.Considerations.Single().Input, 12);
    }
    [Fact]
    public void AlternatingStageKeepsExactlyOneInterceptorAndIsDeterministic()
    {
        var a = Run(); var b = Run();
        Assert.Equal(a, b);
        Assert.Contains(a, x => x.StartsWith("GuardHealer"));
        Assert.Contains(a, x => x.StartsWith("ShieldAdvance"));
        static string[] Run()
        {
            var expedition = SuspicionTests.Create();
            SuspicionTests.Complete(expedition, Observation(9, 8));
            var results = new List<string>();
            for (int encounter = 0; encounter < 8; encounter++)
            {
                expedition.TryStartEncounter(out var tactic);
                var world = expedition.EncounterWorld!;
                Add(world, 1, TargetRole.Attacker, new Vec2(8, 0), Side.Players);
                Add(world, 10, TargetRole.Guard, new Vec2(0, 0));
                Add(world, 11, TargetRole.Guard, new Vec2(0, 1));
                Add(world, 13, TargetRole.Healer, new Vec2(-3, 0));
                var brain = PuppetBrain.Create(world, new PuppetSettings(), new DeterministicRandom(1)).Value;
                for (int tick = 0; tick < 30; tick++)
                {
                    Assert.Equal(AiFailure.None, brain.Tick(tick));
                    Assert.Single(world.GetAgents(), x => x.Role == TargetRole.Guard && x.CurrentAction == ActionIds.Intercept);
                }
                var board = world.GetBlackboard(Side.Puppets).Value;
                results.Add(tactic.Id + ":" + string.Join(",", expedition.PreviousIntentions.Select(p => p.Key + "=" + p.Value))
                    + ":" + board.GetFact<AgentId>(TargetFactIds.HealerThreat).Value.Value
                    + ":" + board.GetFact<double>(TargetFactIds.HealerThreatStrength).Value);
                expedition.EndEncounterWithIndividuals(encounter % 2 == 0 ? Observation(8, 10) : Observation(10, 8));
            }
            return results.ToArray();
        }
    }
    [Fact]
    public void InvalidNewSettingsAreRejected()
    {
        Assert.False(RuleTacticSelector.TryCreate(new TacticSettings(blendFloor: double.NaN), out _));
        foreach (var settings in new[] { new ExpeditionSettings(carryOverRatio: 2), new ExpeditionSettings(maxIntentShiftPerEncounter: double.PositiveInfinity) })
            Assert.False(Expedition.TryCreate(settings, new RuleTacticSelector(), out _));
        Assert.False(PuppetBrain.Create(new World(), new PuppetSettings(individualThreatInterceptScale: -1), new DeterministicRandom(1)).Success);
    }
}
