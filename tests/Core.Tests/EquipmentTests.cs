using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BehindTheCurtain.Core.Sim;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class EquipmentTests
{
    private static Loadout Gear(params string[] ids) => Loadout.Create(ids).Value;
    private static AgentState Agent(int id, TargetRole role, double x = 0, Side side = Side.Puppets) =>
        new(new AgentId(id), side, side == Side.Players, role, new Vec2(x, 0), new Vec2(1, 0), 0.3);
    private static PuppetSettings Settings(double speed = 3) => new(moveSpeed: speed, weightJitter: 0,
        persistenceBonus: 0, reevaluationIntervalTicks: 1, arenaHalfSize: 30);
    private static PuppetBrain Brain(World world, PuppetSettings? settings = null) =>
        PuppetBrain.Create(world, settings ?? Settings(), new DeterministicRandom(1),
            new DirectorProfile(tuneBudgetPerSecond: 0, enableWaver: false)).Value;

    [Theory]
    [InlineData("approach")] [InlineData("circle")] [InlineData("retreat")] [InlineData("healer")]
    public void ImplicitAndExplicitPresetsHaveIdenticalEveryTickTrajectory(string scenario)
    {
        var implicitGear = PuppetSimulation.Run(scenario, 821, null, null, null, "none");
        var explicitGear = PuppetSimulation.Run(scenario, 821, null, null, null, "none", explicitPresets: true);
        EqualTrajectory(implicitGear, explicitGear);
        var mixed = PuppetSimulation.Run(scenario, 821, null, null, null, "none", loadout: "mixed");
        EqualTrajectory(mixed, PuppetSimulation.Run(scenario, 821, null, null, null, "none", loadout: "mixed"));
    }
    private static void EqualTrajectory(PuppetTrajectory a, PuppetTrajectory b)
    {
        Assert.Equal(a.Frames.Count, b.Frames.Count);
        for (int i = 0; i < a.Frames.Count; i++)
        {
            Assert.Equal(a.Frames[i].Player, b.Frames[i].Player);
            Assert.Equal(a.Frames[i].Puppets.Select(p => (p.Id, p.Position, p.CurrentAction, p.Target)),
                b.Frames[i].Puppets.Select(p => (p.Id, p.Position, p.CurrentAction, p.Target)));
        }
    }

    [Theory]
    [InlineData(false, 1)] [InlineData(false, 0.5)] [InlineData(true, 1)] [InlineData(true, 0.5)]
    public void ProtectIsRequiredRegardlessOfRoleIntentionAndTension(bool protect, double tension)
    {
        var world = new World();
        Assert.True(world.Add(Agent(1, TargetRole.Attacker, 5, Side.Players)).Success);
        var initial = new PuppetInitialState(values: new Dictionary<PuppetValueId, double> { [PuppetValueId.Tension] = tension });
        Assert.True(world.Add(Agent(10, TargetRole.Guard), initial, protect ? LoadoutPresets.Shield : LoadoutPresets.Melee).Success);
        Assert.True(world.Add(Agent(13, TargetRole.Healer, -3)).Success);
        var board = world.GetBlackboard(Side.Puppets).Value;
        board.SetFact("tactic", TacticId.GuardHealer); board.SetFact(TargetFactIds.HealerThreat, new AgentId(1));
        board.SetIntention("Guard:intercept", 100);
        // High pull cannot reintroduce a filtered candidate.
        var brain = PuppetBrain.Create(world, Settings(), new DeterministicRandom(1),
            new DirectorProfile(tuneBudgetPerSecond: 0, waverPull: 0)).Value;
        for (int tick = 0; tick < 65; tick++)
        {
            var before = world.Get(new AgentId(10)).Value.Position;
            Assert.Equal(AiFailure.None, brain.Tick(tick));
            var after = world.Get(new AgentId(10)).Value;
            var scores = brain.GetDecision(after.Id).Value.Selection.Scores;
            if (!protect)
            {
                Assert.Equal(0, scores.Single(s => s.ActionId == ActionIds.Intercept).Score);
                Assert.Contains(after.CurrentAction, new[] { ActionIds.Approach, ActionIds.Attack, ActionIds.Pressure });
                Assert.True(after.CurrentAction == ActionIds.Attack || !before.Equals(after.Position));
            }
            else if (tick == 0) Assert.Equal(ActionIds.Intercept, after.CurrentAction);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(100)]
    public void WaverPullCannotOpenInterceptWithoutProtect(double pull)
    {
        var world = new World(); world.Add(Agent(1, TargetRole.Attacker, 5, Side.Players));
        world.Add(Agent(13, TargetRole.Healer, -3));
        world.Add(Agent(10, TargetRole.Guard), new PuppetInitialState(values:
            new Dictionary<PuppetValueId, double> { [PuppetValueId.Tension] = 0.5 }), LoadoutPresets.Melee);
        var board = world.GetBlackboard(Side.Puppets).Value;
        board.SetFact("tactic", TacticId.GuardHealer); board.SetFact(TargetFactIds.HealerThreat, new AgentId(1));
        board.SetIntention("Guard:intercept", 100);
        var brain = PuppetBrain.Create(world, Settings(), new DeterministicRandom(1),
            new DirectorProfile(tuneBudgetPerSecond: 0, waverPull: pull)).Value;
        for (int tick = 0; tick < 65; tick++)
        {
            brain.Tick(tick);
            Assert.NotEqual(ActionIds.Intercept, world.Get(new AgentId(10)).Value.CurrentAction);
            Assert.Equal(0, brain.GetDecision(new AgentId(10)).Value.Selection.Scores.Single(s => s.ActionId == ActionIds.Intercept).Score);
        }
    }

    [Theory]
    [InlineData("weapon.spear", 3, "attack")]
    [InlineData("weapon.spear", 3.5, "attack")]
    [InlineData("weapon.spear", 3.5001, "approach")]
    [InlineData("weapon.bow", 8, "attack")]
    [InlineData("weapon.bow", 8.0001, "approach")]
    public void WeaponRangeControlsAttackAndApproachGoal(string weapon, double distance, string action)
    {
        var world = new World(); world.Add(Agent(1, TargetRole.Attacker, distance, Side.Players));
        world.AddWithLoadout(Agent(10, TargetRole.Attacker), Gear(weapon));
        var settings = Settings(); var brain = Brain(world, settings); brain.Tick(0);
        var decision = brain.GetDecision(new AgentId(10)).Value;
        Assert.Equal(action, decision.Selection.ActionId);
        var range = world.GetAttackRange(new AgentId(10), settings).Value;
        if (action == ActionIds.Attack) Assert.Equal(new Vec2(0, 0), world.Get(new AgentId(10)).Value.Position);
        else Assert.Equal(range - settings.AttackArrivalMargin, PuppetSimulation.Distance(decision.Goal, new Vec2(distance, 0)), 9);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void HeavyShieldMultipliesMovementAndPostureCost(bool staggered)
    {
        double Move(Loadout gear)
        {
            var world = new World(); world.Add(Agent(1, TargetRole.Attacker, 5, Side.Players));
            var initial = new PuppetInitialState(values: new Dictionary<PuppetValueId, double> { [PuppetValueId.Posture] = staggered ? 10 : 100 });
            world.Add(Agent(10, TargetRole.Guard), initial, gear);
            var brain = Brain(world); for (int tick = 0; tick < 10; tick++) brain.Tick(tick);
            return world.Get(new AgentId(10)).Value.Position.X;
        }
        var normal = Move(LoadoutPresets.Shield);
        Assert.Equal(staggered ? 0.5 : 1, normal, 9);
        Assert.Equal(normal * 0.8, Move(Gear("weapon.melee", "armor.heavyShield")), 9);
    }

    [Fact]
    public void InvalidLoadoutsAndDefinitionsAreRejectedAndCollectionsAreCopied()
    {
        foreach (var ids in new[] { Array.Empty<string>(), new[] { "armor.shield" },
            new[] { "weapon.melee", "weapon.bow" }, new[] { "weapon.melee", "armor.shield", "armor.heavyShield" },
            new[] { "weapon.melee", "tool.mender", "tool.jammer" }, new[] { "missing" } })
            Assert.False(Loadout.Create(ids).Success);
        foreach (var cost in new[] { 0, 1.5, double.NaN, double.PositiveInfinity })
            Assert.False(EquipmentCatalog.Create(new[] { new EquipmentDefinition("bad", EquipmentSlot.Weapon, RangeBand.Melee, cost: new EquipmentCost(cost)) }).Success);
        var four = EquipmentCatalog.Create(new[] { new EquipmentDefinition("four", EquipmentSlot.Weapon, RangeBand.Melee,
            Enum.GetValues<CapabilityTag>()) }).Value;
        Assert.False(Loadout.Create(new[] { "four" }, four).Success);
        Assert.False(EquipmentCatalog.Create(new[] { new EquipmentDefinition("bad", EquipmentSlot.Weapon) }).Success);
        Assert.False(EquipmentCatalog.Create(new[] { new EquipmentDefinition("bad", EquipmentSlot.Armor, RangeBand.Melee) }).Success);
        var tags = new List<CapabilityTag> { CapabilityTag.Heal }; var item = new EquipmentDefinition("custom", EquipmentSlot.StageTool, capabilities: tags);
        tags.Clear(); Assert.Single(item.Capabilities);
        Assert.True(EquipmentCatalog.Default.Get("tool.mender").Value.Cost.ImmobileWhileUsing);
        Assert.True(EquipmentCatalog.Default.Get("tool.jammer").Value.Cost.ImmobileWhileUsing);
        Assert.Equal(new[] { CapabilityTag.Block, CapabilityTag.Protect, CapabilityTag.Heal }, Gear("weapon.melee", "armor.shield", "tool.mender").Capabilities);
    }

    [Fact]
    public void EquipmentCostsMultiplyAndImmobileDataDoesNotYetStopBasicActions()
    {
        var catalog = EquipmentCatalog.Create(new[] {
            new EquipmentDefinition("weapon", EquipmentSlot.Weapon, RangeBand.Mid, cost: new EquipmentCost(0.8)),
            new EquipmentDefinition("armor", EquipmentSlot.Armor, cost: new EquipmentCost(0.5)),
            new EquipmentDefinition("tool", EquipmentSlot.StageTool, capabilities: new[] { CapabilityTag.Disrupt }, cost: new EquipmentCost(0.9, true))
        }).Value;
        var gear = Loadout.Create(new[] { "weapon", "armor", "tool" }, catalog).Value;
        Assert.Equal(0.36, gear.MoveSpeedScale, 9);
        var world = new World(); world.Add(Agent(1, TargetRole.Attacker, 5, Side.Players));
        world.AddWithLoadout(Agent(10, TargetRole.Attacker), gear);
        var brain = Brain(world); brain.Tick(0);
        Assert.Equal(0.036, world.Get(new AgentId(10)).Value.Position.X, 9);
        Assert.Equal(ActionIds.Approach, world.Get(new AgentId(10)).Value.CurrentAction);
        Assert.True(world.GetLoadout(new AgentId(10)).Value.HasCapability(CapabilityTag.Disrupt));
        Assert.False(world.AddWithLoadout(Agent(10, TargetRole.Attacker), LoadoutPresets.Melee).Success);
        Assert.Same(gear, world.GetLoadout(new AgentId(10)).Value);
        Assert.False(world.AddWithLoadout(Agent(20, TargetRole.Attacker), null!).Success);
        Assert.False(world.Get(new AgentId(20)).Success);
    }

    [Fact]
    public void PlayerEquipmentIsReadableAndDoesNotAffectPuppetDecisions()
    {
        (Vec2, string?)[] Run(bool equipped)
        {
            var world = new World(); var player = Agent(1, TargetRole.Attacker, 5, Side.Players);
            if (equipped)
            {
                world.AddWithLoadout(player, Gear("weapon.bow", "tool.jammer"));
                Assert.True(world.GetLoadout(player.Id).Value.HasCapability(CapabilityTag.Disrupt));
                Assert.Equal(8, world.GetAttackRange(player.Id, Settings()).Value);
            }
            else world.Add(player);
            world.Add(Agent(10, TargetRole.Attacker)); var brain = Brain(world); var frames = new List<(Vec2, string?)>();
            for (int tick = 0; tick < 100; tick++) { brain.Tick(tick); var agent = world.Get(new AgentId(10)).Value; frames.Add((agent.Position, agent.CurrentAction)); }
            return frames.ToArray();
        }
        Assert.Equal(Run(false), Run(true));
    }

    [Fact]
    public void MixedCliPrintsEquipmentAndRetainsMetrics()
    {
        var output = new StringWriter(); var error = new StringWriter();
        Assert.Equal(0, Simulation.Run(new[] { "--puppets", "healer", "--loadout", "mixed" }, output, error));
        Assert.Contains("11:Guard equipment=weapon.melee capabilities= attackRange=2.00 moveSpeedScale=1.00", output.ToString());
        Assert.Contains("missedAttack=0", output.ToString()); Assert.Contains("pingPong=0", output.ToString());
        Assert.Equal("", error.ToString());
        Assert.Equal(2, Simulation.Run(new[] { "--loadout", "mixed" }, new StringWriter(), error));
        Assert.Equal(2, Simulation.Run(new[] { "--puppets", "healer", "--loadout", "unknown" }, new StringWriter(), error));
    }
}
