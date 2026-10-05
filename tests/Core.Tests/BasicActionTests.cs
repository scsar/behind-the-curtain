using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BehindTheCurtain.Core.Sim;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class BasicActionTests
{
    private static AgentState Agent(int id, Vec2 position, Side side = Side.Puppets, TargetRole role = TargetRole.Attacker,
        string? action = null, AgentId? target = null) => new(new AgentId(id), side, side == Side.Players,
            role, position, new Vec2(0, 1), .3, action, target);
    private static PuppetBrain Brain(World world, PuppetSettings settings) => PuppetBrain.Create(world, settings,
        new DeterministicRandom(1), new DirectorProfile(enableWaver: false, tuneBudgetPerSecond: 0)).Value;
    private static double Distance(Vec2 a, Vec2 b) => PuppetSimulation.Distance(a, b);
    private static PuppetSettings HarassSettings() => new(new BasicActionSettings(flankWeight: 0, harassWeight: 1), weightJitter: 0, reevaluationIntervalTicks: 1,
        approachWeight: 0, attackWeight: 0, spreadWeight: 0);

    [Fact]
    public void FlankRequiresFrontAndTravelsAnArcBeforeAttackAtRightAngle()
    {
        var world = new World();
        world.Add(Agent(1, new Vec2(0, 0), Side.Players));
        world.Add(Agent(10, new Vec2(0, 1.5), role: TargetRole.Guard, action: ActionIds.Attack, target: new AgentId(1)));
        world.Add(Agent(11, new Vec2(0, 4)));
        var brain = Brain(world, new PuppetSettings(weightJitter: 0, reevaluationIntervalTicks: 1));
        bool closing = false; bool arrived = false; int arcTicks = 0;
        for (int tick = 0; tick < 150; tick++)
        {
            Assert.Equal(AiFailure.None, brain.Tick(tick));
            var a = world.Get(new AgentId(11)).Value;
            if (tick == 0) Assert.Equal(ActionIds.Flank, a.CurrentAction);
            double radius = Distance(a.Position, new Vec2(0, 0));
            if (radius < 3.7) closing = true;
            if (!closing) { arcTicks++; Assert.InRange(radius, 3.7, 4.3); }
            if (a.CurrentAction == ActionIds.Attack)
            {
                double angle = Math.Abs(Math.Atan2(a.Position.X, a.Position.Z) * 180 / Math.PI);
                Assert.InRange(angle, 70, 110); arrived = true; break;
            }
        }
        Assert.True(arcTicks > 30); Assert.True(arrived);
        var solo = new World(); solo.Add(Agent(1, new Vec2(0, 0), Side.Players)); solo.Add(Agent(11, new Vec2(0, 4)));
        var soloBrain = Brain(solo, new PuppetSettings()); soloBrain.Tick(0);
        Assert.Equal(0, soloBrain.GetDecision(new AgentId(11)).Value.Selection.Scores.Single(s => s.ActionId == ActionIds.Flank).Product);
    }

    [Theory]
    [InlineData(false, true, 2)]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 1)]
    public void HarassChoosesLeastTargetedThenNearestOrOnlyHuman(bool single, bool occupied, int expected)
    {
        var world = new World(); world.Add(Agent(1, new Vec2(3, 0), Side.Players));
        if (!single) world.Add(Agent(2, new Vec2(6, 0), Side.Players));
        if (occupied) world.Add(Agent(10, new Vec2(2, 0), role: TargetRole.Guard, action: ActionIds.Attack, target: new AgentId(1)));
        world.Add(Agent(11, new Vec2(0, 0)));
        var brain = Brain(world, HarassSettings()); brain.Tick(0);
        Assert.Equal(ActionIds.Harass, world.Get(new AgentId(11)).Value.CurrentAction);
        Assert.Equal(expected, brain.GetDecision(new AgentId(11)).Value.Target!.Value.Value);
    }

    [Fact]
    public void HarassWaitsNinetyTicksStrikesOnceAndRecoversMotionlessForThirty()
    {
        var world = new World(); world.Add(Agent(1, new Vec2(0, 0), Side.Players));
        world.Add(Agent(11, new Vec2(0, 3)));
        var brain = Brain(world, HarassSettings());
        for (int tick = 0; tick < 90; tick++)
        {
            brain.Tick(tick); var d = brain.GetDecision(new AgentId(11)).Value;
            Assert.Equal(HarassPhase.Waiting, d.HarassPhase);
            Assert.Equal(3, Distance(d.Goal, new Vec2(0, 0)), 8);
            Assert.Equal(new Vec2(0, 3), world.Get(new AgentId(11)).Value.Position);
        }
        brain.Tick(90); Assert.Equal(HarassPhase.Strike, brain.GetDecision(new AgentId(11)).Value.HarassPhase);
        int strike = 91;
        for (; strike < 120; strike++)
        {
            brain.Tick(strike);
            if (world.Get(new AgentId(11)).Value.CurrentAction == ActionIds.Attack) break;
        }
        Assert.True(strike < 120);
        var position = world.Get(new AgentId(11)).Value.Position;
        Assert.InRange(Distance(position, new Vec2(0, 0)), 0, 2);
        for (int tick = strike + 1; tick <= strike + 30; tick++)
        {
            brain.Tick(tick); Assert.Equal(HarassPhase.Recover, brain.GetDecision(new AgentId(11)).Value.HarassPhase);
            Assert.Equal(position, world.Get(new AgentId(11)).Value.Position);
            Assert.Equal(ActionIds.Harass, world.Get(new AgentId(11)).Value.CurrentAction);
        }
        brain.Tick(strike + 31); Assert.Equal(HarassPhase.Withdraw, brain.GetDecision(new AgentId(11)).Value.HarassPhase);
        Assert.True(Distance(world.Get(new AgentId(11)).Value.Position, new Vec2(0, 0)) > Distance(position, new Vec2(0, 0)));
    }

    [Fact]
    public void NewActionsAreBasicPrimaryOptionsWithConfiguredWeaponPreferences()
    {
        var contract = RoleContract.ForRole(TargetRole.Attacker);
        Assert.Equal(new[] { ActionIds.Approach, ActionIds.Attack, ActionIds.Flank, ActionIds.Harass }, contract.Actions);
        foreach (string action in new[] { ActionIds.Flank, ActionIds.Harass })
        {
            Assert.True(RoleContract.IsBasicAction(action)); Assert.Equal(ContractActionKind.Primary, contract.Classify(action));
            Assert.True(RoleContract.Create("test", new[] { ActionIds.Intercept, action }).Success);
            Assert.Equal(0, new PuppetSettings().GetRoleWeight(TargetRole.Guard, action));
            Assert.Equal(0, new PuppetSettings().GetRoleWeight(TargetRole.Healer, action));
        }
        foreach (string weapon in new[] { "weapon.melee", "weapon.spear", "weapon.bow" })
        {
            var world = new World(); world.Add(Agent(1, new Vec2(0, 0), Side.Players));
            world.AddWithLoadout(Agent(11, new Vec2(0, 5)), Loadout.Create(new[] { weapon }).Value);
            var brain = Brain(world, new PuppetSettings(weightJitter: 0)); brain.Tick(0);
            var scores = brain.GetDecision(new AgentId(11)).Value.Selection.Scores;
            Assert.Equal(weapon == "weapon.melee" ? .25 : .075, scores.Single(s => s.ActionId == ActionIds.Flank).BaseWeight, 8);
            Assert.Equal(weapon == "weapon.melee" ? .06 : .2, scores.Single(s => s.ActionId == ActionIds.Harass).BaseWeight, 8);
        }
    }

    [Fact]
    public void NineScenariosThreeLoadoutsPreserveForbiddenPatternsOccupancyAndDeterminism()
    {
        var failures = new List<string>();
        foreach (string name in PuppetSimulation.Names)
        foreach (string loadout in new[] { "presets", "mixed", "noprotect" })
        {
            var r = PuppetSimulation.Run(name, 1, null, null, null, "none", loadout == "mixed", loadout: loadout);
            string label = name + "/" + loadout;
            if (r.FleeTicks != 0 || r.JitterFailures != 0 || r.InactivityFailures != 0 || r.PingPongs != 0 || r.MissedAttackWindows != 0
                || r.MaxSwitchesInWindow > 3 || r.SimultaneousRatio >= .2 || r.MaxInterceptors > 1)
                failures.Add($"{label}: flee={r.FleeTicks} jitter={r.JitterFailures} inactive={r.InactivityFailures} pingPong={r.PingPongs} missedAttack={r.MissedAttackWindows} switches={r.MaxSwitchesInWindow} simultaneous={r.SimultaneousRatio}");
            Assert.Equal(r.Frames.Sum(f => f.Puppets.Length), r.ActionAgentTicks.Values.Sum());
            var repeat = PuppetSimulation.Run(name, 1, null, null, null, "none", loadout == "mixed", loadout: loadout);
            Assert.Equal(InterceptRegressionTests.Hash(r), InterceptRegressionTests.Hash(repeat));
            using var first = new StringWriter(); using var second = new StringWriter();
            PuppetSimulation.Print(r, first); PuppetSimulation.Print(repeat, second); Assert.Equal(first.ToString(), second.ToString());
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void MixedCastDuoWeaponActionOccupancyMatchesPreferences()
    {
        var r = PuppetSimulation.Run("duo", 1, null, null, null, "none", true, loadout: "mixed");
        int Count(int id, string action) => r.Frames.Count(f => f.Puppets.Single(a => a.Id.Value == id).CurrentAction == action);
        Assert.True(Count(12, ActionIds.Harass) > Count(12, ActionIds.Flank),
            $"weapon.spear: harass={Count(12, ActionIds.Harass)} flank={Count(12, ActionIds.Flank)}");
        Assert.True(Count(11, ActionIds.Flank) > Count(11, ActionIds.Harass),
            $"weapon.melee: flank={Count(11, ActionIds.Flank)} harass={Count(11, ActionIds.Harass)}");
    }

    [Fact]
    public void DuoSwapHarassReducesConcentrationBelowBaselineButNotToZero()
    {
        var r = PuppetSimulation.Run("duo-swap");
        Assert.True(r.ConcentrationRatio > 0 && r.ConcentrationRatio < .352,
            $"concentrated={r.ConcentratedTicks}/{r.NonHealerTargetTicks} ({r.ConcentrationRatio:P1}); harass={r.ActionAgentTicks.GetValueOrDefault(ActionIds.Harass)}");
    }

    [Theory]
    [InlineData("duo")]
    [InlineData("duo-swap")]
    public void NoProtectDuoJitterAlsoExistsWithBothNewActionsDisabled(string name)
    {
        var baseline = PuppetSimulation.Run(name, 1, null, new PuppetSettings(new BasicActionSettings(flankWeight: 0, harassWeight: 0), arenaHalfSize: 30), null, "none", false, loadout: "noprotect");
        Assert.True(baseline.JitterFailures > 0);
        var current = PuppetSimulation.Run(name, 1, null, null, null, "none", false, loadout: "noprotect");
        Assert.Equal(baseline.JitterFailures, current.JitterFailures);
        Assert.Equal(baseline.InactivityFailures, current.InactivityFailures);
    }
    [Fact]
    public void MissingHumansDisableNewActionsAndInvalidNewTuningIsRejected()
    {
        var world = new World(); world.Add(Agent(11, new Vec2(0, 4)));
        var brain = Brain(world, new PuppetSettings()); brain.Tick(0);
        Assert.All(brain.GetDecision(new AgentId(11)).Value.Selection.Scores.Where(s => s.ActionId == ActionIds.Flank || s.ActionId == ActionIds.Harass),
            s => Assert.Equal(0, s.Product));
        foreach (var invalid in new[] { new BasicActionSettings(flankAngle: 0), new BasicActionSettings(flankAngle: 181),
            new BasicActionSettings(flankRadius: double.NaN), new BasicActionSettings(harassMargin: -1),
            new BasicActionSettings(harassIntervalTicks: 0), new BasicActionSettings(harassRecoverTicks: -1),
            new BasicActionSettings(flankNonMeleeScale: double.PositiveInfinity), new BasicActionSettings(harassMeleeScale: -1),
            new BasicActionSettings(flankWeight: double.NaN), new BasicActionSettings(harassWeight: -1) })
            Assert.Equal(AiFailure.InvalidSettings, PuppetBrain.Create(world, new PuppetSettings(invalid), new DeterministicRandom(1)).Failure);
    }

}
