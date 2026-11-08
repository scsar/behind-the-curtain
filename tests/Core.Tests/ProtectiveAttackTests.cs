using System.Linq;
using BehindTheCurtain.Core.Sim;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public class ProtectiveAttackTests
{
    [Theory]
    [InlineData(false, -0.001, 0)]
    [InlineData(false, 0.001, 1)]
    [InlineData(true, 0.001, 1)]
    [InlineData(true, 0, 0)]
    public void HoldEntryAndExitUseSeparateStrictBoundaries(bool holding, double offset, double expected)
    {
        var settings = new PuppetSettings(moveSpeed: 0, weightJitter: 0);
        double distance = settings.ThreatRadius + (holding ? 0 : settings.HoldEntryMargin) + offset;
        var w = new World();
        var id = new AgentId(10);
        w.Add(new AgentState(id, Side.Puppets, false, TargetRole.Guard, new Vec2(0, 0), new Vec2(0, 1), .3));
        w.Add(new AgentState(new AgentId(1), Side.Players, true, TargetRole.Attacker, new Vec2(0, distance), new Vec2(0, 1), .3));
        if (holding) w.UpdateAction(id, ActionIds.Hold, null);
        var b = PuppetBrain.Create(w, settings, new DeterministicRandom(1)).Value;
        Assert.Equal(AiFailure.None, b.Tick(0));
        Assert.Equal(expected, b.GetDecision(id).Value.Selection.Scores.Single(s => s.ActionId == ActionIds.Hold).Product);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(6, false)]
    public void ProtectiveAttackInheritsOnlyWhileBetweenHealerAndThreat(double guardZ, bool inherits)
    {
        var w = new World();
        var id = new AgentId(10);
        w.Add(new AgentState(id, Side.Puppets, false, TargetRole.Guard, new Vec2(0, guardZ), new Vec2(0, 1), .3));
        w.Add(new AgentState(new AgentId(13), Side.Puppets, false, TargetRole.Healer, new Vec2(0, 0), new Vec2(0, 1), .3));
        w.Add(new AgentState(new AgentId(1), Side.Players, true, TargetRole.Attacker, new Vec2(0, 5), new Vec2(0, 1), .3));
        var board = w.GetBlackboard(Side.Puppets).Value;
        board.SetFact(TargetFactIds.HealerThreat, new AgentId(1));
        board.SetFact("tactic", TacticId.GuardHealer);
        w.UpdateAction(id, ActionIds.Intercept, new AgentId(1));
        w.ReduceTension(id, .6, 0);
        var settings = new PuppetSettings(moveSpeed: 0, weightJitter: 0);
        var b = PuppetBrain.Create(w, settings, new DeterministicRandom(1), new DirectorProfile(tuneBudgetPerSecond: 0)).Value;
        Assert.Equal(AiFailure.None, b.Tick(0));
        var d = b.GetDecision(id).Value;
        var attack = d.Selection.Scores.Single(s => s.ActionId == ActionIds.Attack);
        var intercept = d.Selection.Scores.Single(s => s.ActionId == ActionIds.Intercept);
        if (inherits)
        {
            double donor = intercept.BaseWeight * intercept.IntentionMultiplier + settings.PersistenceBonus;
            Assert.Equal(donor, attack.InheritedRoleScore, 12);
            Assert.True(attack.RoleShare / .4 >= donor);
            Assert.Equal(ActionIds.Attack, d.Selection.ActionId);
            Assert.Equal(0, intercept.Score);
            Assert.Equal("protective attack(막아서기 상속)", d.Cause!.Reason);
            Assert.True(attack.Score > d.Selection.Scores.Single(s => s.ActionId == ActionIds.Waver).Score);
        }
        else
        {
            Assert.Equal(0, attack.InheritedRoleScore);
            Assert.Equal(.4 * attack.Product * attack.BaseWeight * attack.IntentionMultiplier, attack.RoleShare, 12);
            Assert.Equal(ActionIds.Intercept, d.Selection.ActionId);
        }
    }

    [Fact]
    public void ProtectiveAttackTraceIsVisibleAndDeterministic()
    {
        var first = PuppetSimulation.Run("healer", "mixed", stress: "tension");
        var repeat = PuppetSimulation.Run("healer", "mixed", stress: "tension");
        Assert.Equal(InterceptRegressionTests.Hash(first), InterceptRegressionTests.Hash(repeat));
        Assert.Contains(first.Frames, f => f.Tick >= 120 && f.DecisionReasons.ContainsKey(11));
        Assert.Equal(first.Frames.SelectMany(f => f.DecisionReasons.Select(p => (f.Tick, p.Key, p.Value))),
            repeat.Frames.SelectMany(f => f.DecisionReasons.Select(p => (f.Tick, p.Key, p.Value))));
        using var output = new System.IO.StringWriter();
        PuppetSimulation.Print(first, output);
        Assert.Contains("protective attack(막아서기 상속)", output.ToString());
    }
}
