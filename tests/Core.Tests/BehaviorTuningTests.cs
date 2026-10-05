using System;
using System.Linq;
using BehindTheCurtain.Core.Sim;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public class BehaviorTuningTests
{
    [Fact]
    public void HarassWithdrawCompletionReportsTheMovingTickAsWithdraw()
    {
        var world = new World();
        world.Add(Agent(1, TargetRole.Attacker, new Vec2(0, 0), Side.Players));
        world.AddWithLoadout(Agent(12, TargetRole.Attacker, new Vec2(0, 4.48)), Loadout.Create(new[] { "weapon.spear" }).Value);
        var settings = new PuppetSettings(new BasicActionSettings(flankWeight: 0, harassWeight: 1),
            approachWeight: 0, attackWeight: 0, weightJitter: 0, reevaluationIntervalTicks: 1);
        var brain = PuppetBrain.Create(world, settings, new DeterministicRandom(1)).Value;
        brain.Tick(0);
        Assert.Equal(HarassPhase.Withdraw, brain.GetDecision(new AgentId(12)).Value.HarassPhase);
        Assert.Equal(new Vec2(0, 4.5), world.Get(new AgentId(12)).Value.Position);
        brain.Tick(1);
        Assert.Equal(HarassPhase.Waiting, brain.GetDecision(new AgentId(12)).Value.HarassPhase);
        Assert.Equal(new Vec2(0, 4.5), world.Get(new AgentId(12)).Value.Position);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HarassWaitingYieldsToEnabledOrdinaryAttackIncludingAnotherHuman(bool anotherHuman)
    {
        var world = new World();
        world.Add(Agent(1, TargetRole.Attacker, new Vec2(0, 0), Side.Players));
        world.Add(Agent(12, TargetRole.Attacker, new Vec2(0, 3)));
        var settings = new PuppetSettings(new BasicActionSettings(flankWeight: 0, harassWeight: 1),
            approachWeight: 0, weightJitter: 0, reevaluationIntervalTicks: 1);
        var brain = PuppetBrain.Create(world, settings, new DeterministicRandom(1)).Value;
        brain.Tick(0);
        Assert.Equal(HarassPhase.Waiting, brain.GetDecision(new AgentId(12)).Value.HarassPhase);
        if (anotherHuman) world.Add(Agent(2, TargetRole.Attacker, new Vec2(0, 4), Side.Players));
        else world.UpdatePosition(new AgentId(1), new Vec2(0, 1.5), new Vec2(0, 1));
        brain.Tick(1);
        var decision = brain.GetDecision(new AgentId(12)).Value;
        Assert.Equal(ActionIds.Attack, decision.Selection.ActionId);
        Assert.Null(decision.HarassPhase);
        Assert.Equal(anotherHuman ? 2 : 1, decision.Target!.Value.Value);
    }

    [Fact]
    public void HarassDoesNotYieldToOrdinaryAttackDisabledByIntention()
    {
        var world = new World();
        world.Add(Agent(1, TargetRole.Attacker, new Vec2(0, 0), Side.Players));
        world.Add(Agent(12, TargetRole.Attacker, new Vec2(0, 3)));
        world.GetBlackboard(Side.Puppets).Value.SetIntention("Attacker:attack", 0);
        var brain = PuppetBrain.Create(world,
            new PuppetSettings(new BasicActionSettings(flankWeight: 0, harassWeight: 1), approachWeight: 0, weightJitter: 0),
            new DeterministicRandom(1)).Value;
        brain.Tick(0);
        world.UpdatePosition(new AgentId(1), new Vec2(0, 1.5), new Vec2(0, 1));
        brain.Tick(1);
        Assert.Equal(ActionIds.Harass, world.Get(new AgentId(12)).Value.CurrentAction);
        Assert.Equal(HarassPhase.Withdraw, brain.GetDecision(new AgentId(12)).Value.HarassPhase);
    }

    [Fact]
    public void HarassOrdinaryAttackWaitsForFullScheduledStrikeRecovery()
    {
        var world = new World();
        world.Add(Agent(1, TargetRole.Attacker, new Vec2(0, 0), Side.Players));
        world.Add(Agent(12, TargetRole.Attacker, new Vec2(0, 3)));
        var settings = new PuppetSettings(new BasicActionSettings(flankWeight: 0, harassWeight: 1),
            approachWeight: 0, weightJitter: 0, reevaluationIntervalTicks: 1);
        var brain = PuppetBrain.Create(world, settings, new DeterministicRandom(1)).Value;
        int tick = 0;
        for (; tick < 120; tick++)
        {
            brain.Tick(tick);
            if (brain.GetDecision(new AgentId(12)).Value.HarassPhase == HarassPhase.Recover) break;
        }
        Assert.InRange(tick, 90, 119);
        for (int t = tick + 1; t <= tick + settings.HarassRecoverTicks; t++)
        {
            brain.Tick(t);
            Assert.Equal(HarassPhase.Recover, brain.GetDecision(new AgentId(12)).Value.HarassPhase);
            Assert.Equal(ActionIds.Harass, world.Get(new AgentId(12)).Value.CurrentAction);
        }
        brain.Tick(tick + settings.HarassRecoverTicks + 1);
        Assert.Equal(ActionIds.Attack, world.Get(new AgentId(12)).Value.CurrentAction);
        Assert.Null(brain.GetDecision(new AgentId(12)).Value.HarassPhase);
    }

    [Theory]
    [InlineData("healer", .8)]
    [InlineData("orbit", 1)]
    public void RequiredInterceptQualityDoesNotRegressWithOrWithoutNewActions(string name, double minimum)
    {
        foreach (bool disabled in new[] { false, true })
        {
            var settings = disabled ? new PuppetSettings(new BasicActionSettings(flankWeight: 0, harassWeight: 0), arenaHalfSize: 30) : null;
            var r = PuppetSimulation.Run(name, settings: settings);
            Assert.True(r.InterceptQualityRatio >= minimum);
            if (name == "orbit") Assert.Equal(9.2, r.InterceptTravel, 8);
        }
    }

    [Theory]
    [InlineData("weapon.melee", 1)]
    [InlineData("weapon.spear", .5)]
    [InlineData("weapon.bow", .5)]
    public void ApproachUsesConfiguredNonMeleeScale(string weapon, double scale)
    {
        double Weight(double configured)
        {
            var world = new World();
            world.Add(Agent(1, TargetRole.Attacker, new Vec2(0, 12), Side.Players));
            world.AddWithLoadout(Agent(11, TargetRole.Attacker, new Vec2(0, 0)), Loadout.Create(new[] { weapon }).Value);
            var settings = new PuppetSettings(new BasicActionSettings(approachNonMeleeScale: configured), weightJitter: 0);
            var brain = PuppetBrain.Create(world, settings, new DeterministicRandom(1)).Value;
            brain.Tick(0);
            return brain.GetDecision(new AgentId(11)).Value.Selection.Scores.Single(s => s.ActionId == ActionIds.Approach).BaseWeight;
        }
        Assert.Equal(.2 * scale, Weight(.5), 8);
        Assert.Equal(.2 * (weapon == "weapon.melee" ? 1 : .25), Weight(.25), 8);
    }

    [Fact]
    public void NoProtectDuoPressureGuardReachesHealerThreatBWithinFiveSeconds()
    {
        var settings = new PuppetSettings(arenaHalfSize: new PuppetScenarioSettings().ArenaHalfSize);
        var r = PuppetSimulation.Run("duo", 1, null, settings, null, "none", loadout: "noprotect");
        var guards = r.Frames[0].Puppets.Where(a => a.Role == TargetRole.Guard && a.CurrentAction == ActionIds.Pressure).ToArray();
        Assert.NotEmpty(guards);
        foreach (var guard in guards)
            Assert.Contains(r.Frames.Where(f => f.Tick <= 5 * settings.TicksPerSecond), f =>
                PuppetSimulation.Distance(f.Puppets.Single(a => a.Id.Equals(guard.Id)).Position,
                    f.Players.Single(a => a.Id.Value == 2).Position) <= settings.AttackRange);
        Assert.Equal(0, r.FleeTicks);
    }

    [Theory]
    [InlineData("duo")]
    [InlineData("duo-swap")]
    public void NoProtectDuoHealerStaysBelowReversalAndInactivityLimits(string name)
    {
        // Also replaces the old assertion that the disabled-action baseline must jitter.
        foreach (bool disabled in new[] { false, true })
        {
            var settings = disabled ? new PuppetSettings(new BasicActionSettings(flankWeight: 0, harassWeight: 0), arenaHalfSize: 30) : null;
            var r = PuppetSimulation.Run(name, 1, null, settings, null, "none", loadout: "noprotect");
            Assert.InRange(r.MaxDirectionReversals[13], 0, 3);
            Assert.True(r.LongestInactiveTicks < 30);
            Assert.Equal(0, r.InactivityFailures);
            Assert.Equal(0, r.JitterFailures);
        }
    }

    [Theory]
    [InlineData(5, 20, 20, true)]
    [InlineData(8, 20, 20, true)]
    [InlineData(6, 19.9, 20, true)]
    [InlineData(6, 20.1, 20, false)]
    [InlineData(6, 10.1, 10, false)]
    [InlineData(4.9, 0, 20, false)]
    [InlineData(8.1, 0, 20, false)]
    public void UnreservedHealerHoldsOnlyInsideAlignedBand(double radius, double angle, double tolerance, bool holds)
    {
        var world = new World();
        world.Add(Agent(1, TargetRole.Attacker, new Vec2(0, 0), Side.Players));
        world.Add(Agent(10, TargetRole.Guard, new Vec2(0, 3)));
        var position = new Vec2(radius * Math.Sin(angle * Math.PI / 180), radius * Math.Cos(angle * Math.PI / 180));
        world.Add(Agent(13, TargetRole.Healer, position));
        world.GetBlackboard(Side.Puppets).Value.SetIntention("Healer:keepDistance", 1);
        var settings = new PuppetSettings(new BasicActionSettings(keepAlignTolerance: tolerance), moveSpeed: 0, weightJitter: 0);
        var brain = PuppetBrain.Create(world, settings, new DeterministicRandom(1)).Value;
        brain.Tick(0);
        Assert.Equal(holds, position.Equals(brain.GetDecision(new AgentId(13)).Value.Goal));
    }

    [Fact]
    public void InvalidAddedTuningIsRejected()
    {
        foreach (var actions in new[] { new BasicActionSettings(approachNonMeleeScale: -1),
            new BasicActionSettings(approachNonMeleeScale: double.NaN), new BasicActionSettings(keepAlignTolerance: -1),
            new BasicActionSettings(keepAlignTolerance: 181), new BasicActionSettings(keepAlignTolerance: double.PositiveInfinity) })
            Assert.Equal(AiFailure.InvalidSettings, PuppetBrain.Create(new World(), new PuppetSettings(actions), new DeterministicRandom(1)).Failure);
    }

    private static AgentState Agent(int id, TargetRole role, Vec2 position, Side side = Side.Puppets) =>
        new(new AgentId(id), side, side == Side.Players, role, position, new Vec2(0, 1), .3);
}
