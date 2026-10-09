using System;
using System.Linq;
using BehindTheCurtain.Core.Sim;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public class TargetSelectionTests
{
    private static AgentState Agent(int id, double x, Side side = Side.Puppets,
        string? action = null, int? target = null) => new(new AgentId(id), side, side == Side.Players,
        TargetRole.Attacker, new Vec2(x, 0), new Vec2(0, 1), .3, action, target.HasValue ? new AgentId(target.Value) : null);
    private static PuppetSettings Settings(double attackWeight = 0, double margin = 1.3, int hold = 30,
        int cap = 2, double penalty = .3) => new(new BasicActionSettings(new SurroundSettings(0, 0), flankWeight: 0, harassWeight: 0),
        moveSpeed: 0, spreadWeight: 0, attackWeight: attackWeight, weightJitter: 0, reevaluationIntervalTicks: 1,
        targetSwitchMargin: margin, targetMinHoldTicks: hold, pressCap: cap, crowdPenalty: penalty);
    private static PuppetBrain Brain(World w, PuppetSettings s) => PuppetBrain.Create(w, s, new DeterministicRandom(1),
        new DirectorProfile(enableWaver: false, tuneBudgetPerSecond: 0)).Value;
    private static int Target(PuppetBrain b, int id = 10) => b.GetDecision(new AgentId(id)).Value.Target!.Value.Value;
    private static World Pair(double a = -3, double b = 3)
    {
        var w = new World(); w.Add(Agent(1, a, Side.Players)); w.Add(Agent(2, b, Side.Players)); return w;
    }
    [Fact]
    public void PressureNeverExceedsCapAfterFirstJudgments()
    {
        var w = Pair(); foreach (int id in new[] {10,11,12}) w.Add(Agent(id, 0));
        var s = Settings(); var brain = Brain(w, s);
        for (int tick = 0; tick < 90; tick++)
        {
            Assert.Equal(AiFailure.None, brain.Tick(tick));
            foreach (var group in new[] {10,11,12}.GroupBy(id => Target(brain,id))) Assert.True(group.Count() <= s.PressCap);
        }
    }
    [Fact]
    public void FirstSameTickThirdPuppetCountsEarlierDecisions()
    {
        var w = Pair(); foreach (int id in new[] {10,11,12}) w.Add(Agent(id, 0));
        var b = Brain(w, Settings()); b.Tick(0);
        Assert.Equal(1,Target(b,10)); Assert.Equal(1,Target(b,11)); Assert.Equal(2,Target(b,12));
    }
    [Fact]
    public void TargetHoldPreventsTwoSwitchesWithinThirtyTicksAndMarginBlocksSimilarScores()
    {
        var w = Pair(); w.Add(Agent(10,0)); var b = Brain(w, Settings());
        int? prior = null; int? lastSwitch = null; int switches = 0;
        for (int tick = 0; tick < 120; tick++)
        {
            // First similar scores cross repeatedly, then a strong advantage alternates.
            double distance = tick < 30 ? (tick % 2 == 0 ? 2.9 : 3.1) : (tick % 2 == 0 ? 6 : 2.1);
            w.UpdatePosition(new AgentId(1),new Vec2(-distance,0),new Vec2(0,1));
            if (tick >= 30) w.UpdatePosition(new AgentId(2),new Vec2(tick % 2 == 0 ? 2.1 : 6,0),new Vec2(0,1));
            b.Tick(tick); int target = Target(b);
            if (tick < 30) Assert.Equal(1,target);
            if (prior.HasValue && prior.Value != target)
            {
                if (lastSwitch.HasValue) Assert.True(tick-lastSwitch.Value >= 30);
                lastSwitch = tick; switches++;
            }
            prior = target;
        }
        Assert.True(switches >= 2);
    }
    [Fact]
    public void CrowdedTargetBypassesHoldAndMargin()
    {
        var w = Pair(); w.Add(Agent(10,0)); w.Add(Agent(11,0)); w.Add(Agent(12,0));
        var b = Brain(w,Settings(margin:100,hold:100)); b.Tick(0);
        w.UpdateAction(new AgentId(11),ActionIds.Approach,new AgentId(1));
        w.UpdateAction(new AgentId(12),ActionIds.Approach,new AgentId(1));
        b.Tick(1); Assert.Equal(2,Target(b));
    }
    [Fact]
    public void LeavingThreatRadiusBypassesHoldAndMargin()
    {
        var w = Pair(); w.Add(Agent(10,0)); var b = Brain(w,Settings(margin:100,hold:100)); b.Tick(0);
        w.UpdatePosition(new AgentId(1),new Vec2(-7,0),new Vec2(0,1)); b.Tick(1); Assert.Equal(2,Target(b));
    }
    [Fact]
    public void AttackUsesInRangeHumanWhenRetainedTargetIsOutsideRange()
    {
        var w = Pair(); w.Add(Agent(10,0)); var b = Brain(w,Settings(attackWeight:1)); b.Tick(0); Assert.Equal(1,Target(b));
        w.UpdatePosition(new AgentId(2),new Vec2(1.9,0),new Vec2(0,1)); b.Tick(1);
        Assert.Equal(ActionIds.Attack,b.GetDecision(new AgentId(10)).Value.Selection.ActionId); Assert.Equal(2,Target(b));
    }
    [Fact]
    public void AttackPrefersRetainedTargetEvenWhenAnotherHumanIsCloser()
    {
        var w = Pair(-1.8,1.9); w.Add(Agent(10,0)); var b = Brain(w,Settings(attackWeight:1)); b.Tick(0);
        w.UpdatePosition(new AgentId(2),new Vec2(1.5,0),new Vec2(0,1)); b.Tick(1); Assert.Equal(1,Target(b));
    }
    [Fact]
    public void AttackFallbackScoresAllInRangeHumansInsteadOfTakingNearest()
    {
        var w=Pair(-3,1); w.Add(Agent(3,1.5,Side.Players)); w.Add(Agent(10,0));
        w.Add(Agent(11,0,action:ActionIds.Approach,target:2)); w.Add(Agent(12,0,action:ActionIds.Approach,target:2));
        w.Add(Agent(13,0,action:ActionIds.Approach,target:2));
        w.Add(Agent(14,0,action:ActionIds.Approach,target:3)); w.Add(Agent(15,0,action:ActionIds.Approach,target:3));
        var b=Brain(w,Settings(attackWeight:1)); b.Tick(0);
        Assert.Equal(ActionIds.Attack,b.GetDecision(new AgentId(10)).Value.Selection.ActionId);
        Assert.Equal(3,Target(b));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstEncounterJudgmentHasZeroHarassEvenAfterEarlierSameTickTargets(bool single)
    {
        var w = single ? new World() : Pair();
        if (single) w.Add(Agent(1,-3,Side.Players));
        w.Add(Agent(10,0)); w.Add(Agent(11,0));
        var s = new PuppetSettings(new BasicActionSettings(flankWeight:0,harassWeight:1),weightJitter:0);
        var b = Brain(w,s); b.Tick(0);
        foreach (int id in new[] {10,11})
        {
            var d = b.GetDecision(new AgentId(id)).Value;
            Assert.NotEqual(ActionIds.Harass,d.Selection.ActionId);
            Assert.Equal(0,d.Selection.Scores.Single(x => x.ActionId == ActionIds.Harass).Product);
        }
    }
    [Theory]
    [InlineData(ActionIds.Approach,1,1)]
    [InlineData(ActionIds.Approach,2,2)]
    [InlineData(ActionIds.Attack,2,2)]
    [InlineData(ActionIds.Flank,2,2)]
    [InlineData(ActionIds.Surround,2,2)]
    [InlineData(ActionIds.Harass,2,2)]
    [InlineData(ActionIds.Pressure,2,2)]
    [InlineData(ActionIds.Intercept,2,1)]
    [InlineData(ActionIds.KeepDistance,2,1)]
    public void TargetScoreCountsOnlyPressureAndPenalizesAtCap(string action,int count,int expected)
    {
        var w = Pair(-3,5); w.Add(Agent(10,0));
        for (int i=0;i<count;i++) w.Add(Agent(11+i,0,action:action,target:1));
        var b=Brain(w,Settings()); b.Tick(0); Assert.Equal(expected,Target(b));
    }
    [Fact]
    public void NoThreatRadiusCandidatesFallBackToNearestEvenWhenCrowded()
    {
        var w=Pair(-7,8); w.Add(Agent(10,0));
        w.Add(Agent(11,0,action:ActionIds.Approach,target:1)); w.Add(Agent(12,0,action:ActionIds.Approach,target:1));
        var b=Brain(w,Settings()); b.Tick(0); Assert.Equal(1,Target(b));
    }
    [Theory]
    [InlineData(0,2,.3,1.3,30)]
    [InlineData(4,0,.3,1.3,30)]
    [InlineData(4,2,0,1.3,30)]
    [InlineData(4,2,1.1,1.3,30)]
    [InlineData(4,2,.3,.9,30)]
    [InlineData(4,2,.3,1.3,-1)]
    public void InvalidTargetSettingsAreRejected(double scale,int cap,double penalty,double margin,int hold)
    {
        Assert.Equal(AiFailure.InvalidSettings,PuppetBrain.Create(Pair(),new PuppetSettings(targetDistanceScale:scale,
            pressCap:cap,crowdPenalty:penalty,targetSwitchMargin:margin,targetMinHoldTicks:hold),new DeterministicRandom(1)).Failure);
    }
    [Theory]
    [InlineData("duo","presets","8FF9EF4A2650B110744C912C63A9FCDEEF835671248255713109CA10026B0D62")]
    [InlineData("duo-swap","presets","2453B283F7A6906FC277D27EA1F352464CAD6C67D59268C5E48D73F6B583258E")]
    [InlineData("duo","mixed","109B7B9AA8BC428E1B787DE29DC5E195B423B9AD66E3D30E90984958B457B4F4")]
    [InlineData("duo-swap","mixed","E2307545D6C8A2769BF7F1BBAE534FED965A0A54049AC6C6FF4104106F3DE819")]
    public void DuoTrajectoriesHaveReviewedHashes(string name,string loadout,string expected)
        {
        string actual = InterceptRegressionTests.Hash(PuppetSimulation.Run(name,1,null,null,null,"none",loadout == "mixed",loadout:loadout));
        Assert.True(expected == actual, $"{name}: {actual}");
    }
}
