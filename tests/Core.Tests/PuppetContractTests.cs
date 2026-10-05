using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BehindTheCurtain.Core.Sim;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class PuppetContractTests
{
    public static IntentionSettings RetreatIntentions() => new IntentionSettings(overrides:
        Enum.GetValues<TacticId>().ToDictionary(t => t, t => (IReadOnlyDictionary<string, double>)
            new Dictionary<string, double> { ["Attacker:keepDistance"] = 4 }));

    [Theory]
    [InlineData("approach", false)] [InlineData("circle", false)]
    [InlineData("retreat", false)] [InlineData("healer", false)]
    [InlineData("approach", true)] [InlineData("circle", true)]
    [InlineData("retreat", true)] [InlineData("healer", true)]
    public void TenSecondContracts(string name, bool custom)
    {
        var limits = new PuppetScenarioSettings();
        var result = PuppetSimulation.Run(name, intentions: custom ? RetreatIntentions() : null);
        Assert.True(result.MaxSwitchesInWindow <= limits.SwitchLimit, $"{name}: switches {result.MaxSwitchesInWindow}");
        Assert.Equal(0, result.PingPongs);
        Assert.True(result.SimultaneousRatio < limits.SynchronizationLimit);
        Assert.InRange(result.MaxInterceptors, 0, 1);
        Assert.Equal(0, result.MissedAttackWindows);
        if (!custom)
        {
            Assert.Equal(0, result.FleeTicks);
            if (!result.HasHealerThreat) Assert.True(result.InterceptRatio < limits.InterceptLimit);
        }
        else
        {
            Assert.True(result.LongestOutsideBand[12] <= 30, $"{name}: outside band {result.LongestOutsideBand[12]}");
            Assert.All(result.Frames, f => Assert.Equal(ActionIds.KeepDistance, f.Puppets.Single(a => a.Id.Value == 12).CurrentAction));
        }
    }

    [Theory]
    [InlineData("approach")] [InlineData("circle")] [InlineData("retreat")] [InlineData("healer")]
    public void SameSeedProducesIdenticalCompleteTrajectory(string name)
    {
        var first = PuppetSimulation.Run(name, 821);
        var second = PuppetSimulation.Run(name, 821);
        Assert.Equal(first.Frames.Count, second.Frames.Count);
        for (int i = 0; i < first.Frames.Count; i++)
        {
            Assert.Equal(first.Frames[i].Player, second.Frames[i].Player);
            Assert.Equal(first.Frames[i].Puppets.Select(a => (a.Id, a.Position, a.CurrentAction, a.Target)),
                second.Frames[i].Puppets.Select(a => (a.Id, a.Position, a.CurrentAction, a.Target)));
        }
        var output = new StringWriter(); var error = new StringWriter();
        Assert.Equal(0, Simulation.Run(new[] { "--puppets", name, "--seed", "821" }, output, error));
        Assert.Contains("tick=300", output.ToString());
        Assert.Contains("missedAttack=0", output.ToString());
        Assert.Equal("", error.ToString());
    }

    [Fact]
    public void RolesDifferAndOnlyExplicitlyEnabledAttackerRetreats()
    {
        var baseline = PuppetSimulation.Run("approach");
        Assert.Contains(baseline.Frames, f => new[] { TargetRole.Guard, TargetRole.Attacker, TargetRole.Healer }
            .Select(role => f.Puppets.First(a => a.Role == role).CurrentAction).Distinct().Count() == 3);
        Assert.DoesNotContain(baseline.Frames, f => f.Puppets.Single(a => a.Id.Value == 12).CurrentAction == ActionIds.KeepDistance);
        var custom = PuppetSimulation.Run("approach", intentions: RetreatIntentions());
        Assert.Contains(custom.Frames.Skip(1), f =>
        {
            var current = f.Puppets.Single(a => a.Id.Value == 12);
            var previous = custom.Frames[f.Tick - 1].Puppets.Single(a => a.Id.Value == 12);
            return PuppetSimulation.Distance(current.Position, f.Player) > PuppetSimulation.Distance(previous.Position, f.Player) + 1e-8;
        });
    }

    [Theory]
    [InlineData(0.349, true)] [InlineData(0.35, true)] [InlineData(0.351, false)]
    public void ThreatFactsRespectInclusiveThreshold(double threshold, bool expected)
    {
        // 350 healer observations out of 1000; equality must publish the fact.
        var expedition = SuspicionTests.Create(new ExpeditionSettings(threatFactThreshold: threshold));
        Assert.True(CombatObserver.TryCreate(new ObserverSettings(sustainedRangedStreak: 1), out var observer));
        for (int i = 0; i < 1000; i++) observer!.Observe(new AttackEvent(0,
            i < 350 ? TargetRole.Healer : TargetRole.Guard, i < 350 ? AttackRange.Ranged : AttackRange.Melee, i, new AgentId(1), true));
        SuspicionTests.Complete(expedition, observer!.EndEncounterWithIndividuals());
        Assert.True(expedition.TryStartEncounter(out _));
        Assert.Equal(expected, expedition.EncounterWorld!.GetBlackboard(Side.Puppets).Value.GetFact<AgentId>(TargetFactIds.HealerThreat).Success);
        Assert.Equal(expected, expedition.EncounterWorld!.GetBlackboard(Side.Puppets).Value.GetFact<AgentId>(TargetFactIds.RangedThreat).Success);
    }

    [Fact]
    public void EngageScoreIsConstantUntilAttackTakesOverAndIntentionsPreferRoleKey()
    {
        var world = new World();
        Add(world, 1, TargetRole.Attacker, new Vec2(20, 0), Side.Players);
        Add(world, 10, TargetRole.Attacker, new Vec2(0, 0));
        var board = world.GetBlackboard(Side.Puppets).Value;
        board.SetIntention(ActionIds.Approach, 2);
        board.SetIntention("Attacker:approach", 3);
        var settings = new PuppetSettings(moveSpeed: 0, weightJitter: 0, persistenceBonus: 0, reevaluationIntervalTicks: 1);
        var brain = PuppetBrain.Create(world, settings, new DeterministicRandom(1)).Value;
        brain.Tick(0);
        var first = brain.GetDecision(new AgentId(10)).Value.Selection.Scores.Single(s => s.ActionId == ActionIds.Approach);
        Assert.Equal(3, first.IntentionMultiplier);
        world.UpdatePosition(new AgentId(1), new Vec2(3, 0), new Vec2(0, 1));
        brain.Tick(1);
        Assert.Equal(first.Score, brain.GetDecision(new AgentId(10)).Value.Selection.Scores.Single(s => s.ActionId == ActionIds.Approach).Score);
        world.UpdatePosition(new AgentId(1), new Vec2(2, 0), new Vec2(0, 1));
        brain.Tick(2);
        Assert.Equal(ActionIds.Attack, world.Get(new AgentId(10)).Value.CurrentAction);
        Assert.Equal(0, brain.GetDecision(new AgentId(10)).Value.Selection.Scores.Single(s => s.ActionId == ActionIds.Approach).Score);
        // Legacy key still applies to another role.
        Add(world, 11, TargetRole.Guard, new Vec2(-4, 0));
        var other = PuppetBrain.Create(world, settings, new DeterministicRandom(1)).Value;
        other.Tick(3);
        Assert.Equal(2, other.GetDecision(new AgentId(11)).Value.Selection.Scores.Single(s => s.ActionId == ActionIds.Approach).IntentionMultiplier);
    }

    [Fact]
    public void JitterIsFixedBoundedAndReevaluationIsStaggered()
    {
        var world = new World();
        Add(world, 1, TargetRole.Attacker, new Vec2(20, 0), Side.Players);
        for (int i = 10; i < 14; i++) Add(world, i, TargetRole.Attacker, new Vec2(i - 10, 0));
        var settings = new PuppetSettings(moveSpeed: 0);
        var brain = PuppetBrain.Create(world, settings, new DeterministicRandom(7)).Value;
        brain.Tick(-40);
        var initial = Enumerable.Range(10, 4).Select(id => brain.GetDecision(new AgentId(id)).Value.Selection.Scores.ToArray()).ToArray();
        Assert.True(initial.Select(scores => scores[0].BaseWeight).Distinct().Count() > 1);
        for (int elapsed = 1; elapsed <= settings.ReevaluationIntervalTicks; elapsed++)
        {
            Assert.Equal(AiFailure.None, brain.Tick(-40 + elapsed));
            for (int i = 0; i < 4; i++)
            {
                var selection = brain.GetDecision(new AgentId(i + 10)).Value.Selection;
                Assert.Equal(elapsed % settings.ReevaluationIntervalTicks == i * settings.ReevaluationIntervalTicks / 4, selection.Reevaluated);
                Assert.Equal(initial[i].Select(s => s.BaseWeight), selection.Scores.Select(s => s.BaseWeight));
                Assert.InRange(selection.Scores[0].BaseWeight,
                    settings.ApproachWeight * (1-settings.WeightJitter), settings.ApproachWeight * (1+settings.WeightJitter));
            }
        }
    }

    [Fact]
    public void ReservationRenewsAndAnotherAgentCanTakeOverAfterOwnerStops()
    {
        var world = new World();
        Add(world, 1, TargetRole.Attacker, new Vec2(0, 6), Side.Players);
        Add(world, 10, TargetRole.Guard, new Vec2(-3, 0));
        // Reservation handoff needs Protect even when an Attacker role intention enables intercept.
        Assert.True(world.AddWithLoadout(new AgentState(new AgentId(11), Side.Puppets, false, TargetRole.Attacker,
            new Vec2(3, 0), new Vec2(0, 1), 0.3), LoadoutPresets.Shield).Success);
        Add(world, 13, TargetRole.Healer, new Vec2(0, -1));
        var board = world.GetBlackboard(Side.Puppets).Value;
        board.SetFact("tactic", TacticId.GuardHealer);
        board.SetFact(TargetFactIds.HealerThreat, new AgentId(1));
        board.SetIntention("Attacker:intercept", 5);
        var brain = PuppetBrain.Create(world, new PuppetSettings(moveSpeed: 0, keepDistanceSpeed: 0.01,
            reevaluationIntervalTicks: 1), new DeterministicRandom(1)).Value;
        for (int tick = 0; tick < 5; tick++)
        {
            brain.Tick(tick);
            var reservation = board.GetReservation("intercept:13", tick).Value;
            Assert.Equal(new AgentId(10), reservation.Owner);
            Assert.Equal(tick + 1, reservation.ExpiresAtTick);
            Assert.Single(world.GetAgents(), a => a.CurrentAction == ActionIds.Intercept);
        }
        board.SetIntention("Guard:intercept", 0);
        brain.Tick(5); brain.Tick(6);
        Assert.Equal(new AgentId(11), board.GetReservation("intercept:13", 6).Value.Owner);
        Assert.Equal(ActionIds.Intercept, world.Get(new AgentId(11)).Value.CurrentAction);
    }

    [Fact]
    public void KeepDistanceTargetsNearestGuardsRearAndHoldsSafeBand()
    {
        var world = new World();
        Add(world, 1, TargetRole.Attacker, new Vec2(0, 0), Side.Players);
        Add(world, 10, TargetRole.Guard, new Vec2(0, 3));
        Add(world, 11, TargetRole.Guard, new Vec2(-8, 0));
        Add(world, 13, TargetRole.Healer, new Vec2(2, 6));
        var brain = PuppetBrain.Create(world, new PuppetSettings(moveSpeed: 0), new DeterministicRandom(1)).Value;
        brain.Tick(0);
        Assert.Equal(new Vec2(0, 5), brain.GetDecision(new AgentId(13)).Value.Goal);
        for (int tick = 1; tick < 100; tick++)
        {
            brain.Tick(tick);
            Assert.InRange(PuppetSimulation.Distance(world.Get(new AgentId(13)).Value.Position, new Vec2(0, 0)), 5 - 1e-8, 8);
        }
    }

    [Fact]
    public void RoleTablesAreCopiedAndInvalidTuningIsRejected()
    {
        var row = new Dictionary<string, double> { [ActionIds.KeepDistance] = 2 };
        var tables = new Dictionary<TargetRole, IReadOnlyDictionary<string, double>> { [TargetRole.Attacker] = row };
        var settings = new PuppetSettings(roleWeights: tables);
        row[ActionIds.KeepDistance] = 9;
        Assert.Equal(2, settings.GetRoleWeight(TargetRole.Attacker, ActionIds.KeepDistance));
        Assert.All(settings.RoleWeights.Values, r => { Assert.Equal(10, r.Count); Assert.Equal(0, r[ActionIds.Waver]); });
        Assert.Equal(0, new PuppetSettings().GetRoleWeight(TargetRole.Attacker, ActionIds.KeepDistance));
        foreach (var invalid in new[] {
            new PuppetSettings(weightJitter: double.NaN), new PuppetSettings(weightJitter: 1),
            new PuppetSettings(keepMin: 8, keepMax: 5), new PuppetSettings(keepMax: double.PositiveInfinity),
            new PuppetSettings(threatRadius: 0), new PuppetSettings(reevaluationIntervalTicks: 0),
            new PuppetSettings(keepDistanceSpeed: -1), new PuppetSettings(passiveInterceptScale: -1),
            new PuppetSettings(attackArrivalMargin: 2), new PuppetSettings(guardHoldScale: double.PositiveInfinity) })
            Assert.Equal(AiFailure.InvalidSettings, PuppetBrain.Create(new World(), invalid, new DeterministicRandom(1)).Failure);
        row[ActionIds.KeepDistance] = double.NaN;
        Assert.Equal(AiFailure.InvalidSettings, PuppetBrain.Create(new World(), new PuppetSettings(roleWeights: tables), new DeterministicRandom(1)).Failure);
        Assert.False(Expedition.TryCreate(new ExpeditionSettings(threatFactThreshold: double.NaN), new RuleTacticSelector(), out _));
    }

    [Fact]
    public void ExplicitZeroRoleIntentionWinsOverLegacyAndTablesAreCopied()
    {
        var row = new Dictionary<string, double> { ["Attacker:approach"] = 0 };
        var intentions = new IntentionSettings(overrides: new Dictionary<TacticId, IReadOnlyDictionary<string, double>> {
            [TacticId.StandardFormation] = row });
        row["Attacker:approach"] = 10;
        Assert.Equal(0, intentions.Tables[TacticId.StandardFormation]["Attacker:approach"]);
        var expedition = SuspicionTests.Create(new ExpeditionSettings(intentions: intentions));
        expedition.TryStartEncounter(out _);
        var world = expedition.EncounterWorld!;
        Add(world, 1, TargetRole.Attacker, new Vec2(9, 0), Side.Players);
        Add(world, 10, TargetRole.Attacker, new Vec2(0, 0));
        world.GetBlackboard(Side.Puppets).Value.SetIntention(ActionIds.Approach, 10);
        // Explicit runtime role intention; the first encounter now starts at neutral multipliers.
        world.GetBlackboard(Side.Puppets).Value.SetIntention("Attacker:approach", 0);
        var brain = PuppetBrain.Create(world, new PuppetSettings(), new DeterministicRandom(1)).Value;
        brain.Tick(0);
        Assert.Equal(0, brain.GetDecision(new AgentId(10)).Value.Selection.Scores.Single(s => s.ActionId == ActionIds.Approach).Score);
    }

    [Theory]
    [InlineData("unknown")] [InlineData("")]
    public void InvalidScenarioIsRejectedByCli(string value)
    {
        var error = new StringWriter();
        Assert.Equal(2, Simulation.Run(new[] { "--puppets", value }, new StringWriter(), error));
        Assert.Contains("approach|circle|retreat|healer", error.ToString());
    }

    private static void Add(World world, int id, TargetRole role, Vec2 position, Side side = Side.Puppets) =>
        Assert.True(world.Add(new AgentState(new AgentId(id), side, side == Side.Players, role, position, new Vec2(0, 1), 0.3)).Success);
}
