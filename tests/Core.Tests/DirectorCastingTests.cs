using System;
using System.IO;
using System.Linq;
using BehindTheCurtain.Core.Sim;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class DirectorCastingTests
{
    private static void Add(World world, int id, Loadout loadout, TargetRole role = TargetRole.Guard) =>
        Assert.True(world.AddWithLoadout(new AgentState(new AgentId(id), Side.Puppets, false, role,
            new Vec2(id, 0), new Vec2(0, 1), 0.3), loadout).Success);

    [Fact]
    public void CastingUsesCapabilitiesIdOrderAndOneRolePerAgent()
    {
        var world = new World();
        Add(world, 5, LoadoutPresets.Shield); Add(world, 3, LoadoutPresets.Shield);
        Add(world, 2, LoadoutPresets.Shield); Add(world, 1, LoadoutPresets.Melee);
        var roles = DirectorCasting.Assign(world).Value;
        Assert.Equal(new[] { TargetRole.Attacker, TargetRole.Guard, TargetRole.Guard, TargetRole.Attacker },
            world.GetAgents().Select(a => a.Role));
        Assert.Equal(4, roles.Count);
        Assert.DoesNotContain(roles.Values, r => r == TargetRole.Healer);
        Assert.Equal(roles, DirectorCasting.Assign(world).Value);
        Assert.All(DirectorCasting.Assign(world, new DirectorCastingSettings(0)).Value.Values, r => Assert.Equal(TargetRole.Attacker, r));
        Assert.Equal(AiFailure.InvalidSettings, DirectorCasting.Assign(world, new DirectorCastingSettings(-1)).Failure);
    }

    [Fact]
    public void EmptyCastingAndUnprotectedCastingHaveNoGuardsAndPreserveOtherState()
    {
        Assert.Empty(DirectorCasting.Assign(new World()).Value);
        var world = new World();
        Add(world, 10, LoadoutPresets.Melee);
        Add(world, 11, LoadoutPresets.Mender);
        var before = world.Get(new AgentId(10)).Value;
        var roles = DirectorCasting.Assign(world).Value;
        Assert.Equal(TargetRole.Attacker, roles[new AgentId(10)]);
        Assert.Equal(TargetRole.Healer, roles[new AgentId(11)]);
        Assert.DoesNotContain(roles.Values, r => r == TargetRole.Guard);
        var after = world.Get(before.Id).Value;
        Assert.NotSame(before, after);
        Assert.Equal(before.Position, after.Position); Assert.Equal(before.Direction, after.Direction);
        Assert.Equal(before.Radius, after.Radius); Assert.Equal(before.IsHuman, after.IsHuman);
        Assert.Same(LoadoutPresets.Melee, world.GetLoadout(before.Id).Value);
    }

    [Fact]
    public void PressureTracksTheThreatRatherThanTheNearestHumanAndDoesNotReserve()
    {
        var world = new World();
        Add(world, 10, LoadoutPresets.Melee);
        Add(world, 11, LoadoutPresets.Melee);
        foreach (var id in new[] { 1, 2 })
            world.Add(new AgentState(new AgentId(id), Side.Players, true, TargetRole.Attacker,
                new Vec2(10, id == 1 ? 4 : 8), new Vec2(0, 1), 0.3));
        var board = world.GetBlackboard(Side.Puppets).Value;
        board.SetFact(TargetFactIds.HealerThreat, new AgentId(2));
        var brain = PuppetBrain.Create(world, new PuppetSettings(weightJitter: 0), new DeterministicRandom(1)).Value;
        brain.Tick(0);
        foreach (var id in new[] { 10, 11 })
        {
            var decision = brain.GetDecision(new AgentId(id)).Value;
            Assert.Equal(ActionIds.Pressure, decision.Selection.ActionId);
            Assert.Equal(new AgentId(2), decision.Target);
            Assert.Equal(1.5, decision.Selection.Scores.Single(s => s.ActionId == ActionIds.Pressure).BaseWeight);
        }
        Assert.False(board.GetReservation("intercept:13", 0).Success);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LowestHealerWinsAndOtherHealerCanGuard(bool protect)
    {
        var world = new World();
        Add(world, 9, protect ? Loadout.Create(new[] { "weapon.short", "tool.mender", "armor.heavyShield" }).Value : LoadoutPresets.ForRole(TargetRole.Healer));
        Add(world, 2, LoadoutPresets.ForRole(TargetRole.Healer));
        var roles = DirectorCasting.Assign(world).Value;
        Assert.Equal(TargetRole.Healer, roles[new AgentId(2)]);
        Assert.Equal(protect ? TargetRole.Guard : TargetRole.Attacker, roles[new AgentId(9)]);
        Assert.Equal(protect ? 1 : 0, roles.Values.Count(r => r == TargetRole.Guard));
    }

    [Fact]
    public void ContractValidatesCopiesAndClassifies()
    {
        Assert.False(RoleContract.Create("goal", new[] { ActionIds.Approach, ActionIds.Intercept }).Success);
        Assert.False(RoleContract.Create("goal", Array.Empty<string>()).Success);
        var actions = new[] { ActionIds.Intercept, ActionIds.Pressure };
        var contract = RoleContract.Create("goal", actions).Value;
        actions[1] = ActionIds.Intercept;
        Assert.Equal(ActionIds.Pressure, contract.Actions[1]);
        Assert.Equal(ContractActionKind.Alternative, contract.Classify(ActionIds.Pressure));
        Assert.Equal(ContractActionKind.Outside, contract.Classify(ActionIds.Attack));
        Assert.Equal(ContractActionKind.Primary, RoleContract.ForRole(TargetRole.Attacker).Classify(ActionIds.Attack));
    }

    [Theory]
    [InlineData(false, true)] [InlineData(false, false)] [InlineData(true, true)]
    public void GuardFallsBackWithoutProtect(bool protect, bool threat)
    {
        var world = new World();
        Add(world, 10, protect ? LoadoutPresets.Shield : LoadoutPresets.Melee);
        Add(world, 13, LoadoutPresets.ForRole(TargetRole.Healer), TargetRole.Healer);
        world.Add(new AgentState(new AgentId(1), Side.Players, true, TargetRole.Attacker,
            new Vec2(10, 5), new Vec2(0, 1), 0.3));
        var board = world.GetBlackboard(Side.Puppets).Value;
        if (threat) board.SetFact(TargetFactIds.HealerThreat, new AgentId(1));
        board.SetFact("tactic", TacticId.GuardHealer);
        var brain = PuppetBrain.Create(world, new PuppetSettings(weightJitter: 0), new DeterministicRandom(1)).Value;
        brain.Tick(0);
        var decision = brain.GetDecision(new AgentId(10)).Value;
        Assert.Equal(protect ? ActionIds.Intercept : threat ? ActionIds.Pressure : ActionIds.Approach, decision.Selection.ActionId);
        Assert.Equal(TargetRole.Guard, decision.Role);
        Assert.Equal("보호 대상의 안전", decision.Contract.Goal);
        Assert.Equal(protect ? ContractActionKind.Primary : ContractActionKind.Alternative, decision.ContractActionKind);
        Assert.Equal(protect ? 0 : threat ? 1 : 2, decision.ContractActionIndex);
        if (!protect) Assert.Equal(0, decision.Selection.Scores.Single(s => s.ActionId == ActionIds.Intercept).Score);
    }

    [Theory]
    [InlineData("approach")] [InlineData("circle")] [InlineData("retreat")] [InlineData("healer")]
    public void PresetCastingRetainsTrajectoryAndFallbackPassesForbiddenPatterns(string name)
    {
        var baseline = PuppetSimulation.Run(name);
        var cast = PuppetSimulation.Run(name, 1, null, null, null, "none", cast: true);
        var again = PuppetSimulation.Run(name, 1, null, null, null, "none", loadout: "noprotect");
        var repeated = PuppetSimulation.Run(name, 1, null, null, null, "none", loadout: "noprotect");
        for (int i = 0; i < baseline.Frames.Count; i++)
        {
            Assert.Equal(baseline.Frames[i].Puppets.Select(a => (a.Id, a.Role, a.Position, a.Direction, a.CurrentAction, a.Target)),
                cast.Frames[i].Puppets.Select(a => (a.Id, a.Role, a.Position, a.Direction, a.CurrentAction, a.Target)));
            Assert.Equal(again.Frames[i].Puppets.Select(a => (a.Id, a.Position, a.CurrentAction, a.Target)),
                repeated.Frames[i].Puppets.Select(a => (a.Id, a.Position, a.CurrentAction, a.Target)));
        }
        foreach (var result in new[] { baseline, cast, again })
        {
            Assert.Equal(0, result.InactivityFailures); Assert.Equal(0, result.FleeTicks);
            Assert.Equal(0, result.PingPongs); Assert.Equal(0, result.MissedAttackWindows);
            Assert.True(result.MaxSwitchesInWindow <= 3); Assert.True(result.SimultaneousRatio < 0.2);
        }
        Assert.DoesNotContain(again.Frames.SelectMany(f => f.Puppets), a => a.CurrentAction == ActionIds.Intercept);
    }

    [Fact]
    public void InactivityMetricDetectsAnIncapableGuardThatCannotMove()
    {
        var result = PuppetSimulation.Run("healer", 1, null,
            new PuppetSettings(moveSpeed: 0), null, "none", loadout: "noprotect");
        Assert.True(result.InactivityFailures > 0);
        Assert.True(result.LongestInactiveTicks > 30);
    }

    [Fact]
    public void CliCastingIsOptionalAndMixedDoesNotCastUnprotectedGuards()
    {
        var output = new StringWriter();
        Assert.Equal(0, Simulation.Run(new[] { "--puppets", "healer", "--loadout", "mixed", "--cast" }, output, new StringWriter()));
        Assert.Contains("11:Attacker equipment=weapon.melee", output.ToString());
        Assert.Contains("goal=위협자 압박", output.ToString());
        Assert.Equal(2, Simulation.Run(new[] { "--cast" }, new StringWriter(), new StringWriter()));
    }
}
