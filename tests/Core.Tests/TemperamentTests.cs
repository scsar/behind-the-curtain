using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BehindTheCurtain.Core.Sim;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public class TemperamentTests
{
    private static Temperament Axes(double b = 0, double p = 0) => new(new Dictionary<string, double>
    { [TemperamentAxisIds.Boldness] = b, [TemperamentAxisIds.Persistence] = p });

    private static World World()
    {
        var world = new World();
        world.Add(new AgentState(new AgentId(10), Side.Puppets, false, TargetRole.Guard, new Vec2(0, 0), new Vec2(0, 1), .3));
        world.Add(new AgentState(new AgentId(1), Side.Players, true, TargetRole.Attacker, new Vec2(0, 6), new Vec2(0, 1), .3));
        return world;
    }

    [Fact]
    public void OpenAxesAreCopiedAndMissingAxesAreZero()
    {
        var values = new Dictionary<string, double> { ["future"] = .7 };
        var temperament = new Temperament(values); values["future"] = -1;
        var world = World();
        Assert.True(world.SetTemperament(new AgentId(10), temperament).Success);
        Assert.Equal(.7, world.GetTemperament(new AgentId(10)).Value["future"]);
        Assert.Equal(0, temperament[TemperamentAxisIds.Boldness]);
        Assert.Equal(0, world.GetTemperament(new AgentId(10)).Value["missing"]);
    }

    [Theory]
    [InlineData(-1.01)] [InlineData(1.01)] [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public void InvalidValuesAreRejectedWithoutMutation(double value)
    {
        var world = World(); var id = new AgentId(10);
        Assert.True(world.SetTemperament(id, Axes(.5)).Success);
        Assert.Equal(AiFailure.InvalidValue, world.SetTemperament(id, Axes(value)).Failure);
        Assert.Equal(.5, world.GetTemperament(id).Value[TemperamentAxisIds.Boldness]);
    }

    [Fact]
    public void InvalidAgentsAndNullAreRejected()
    {
        var world = World();
        foreach (int id in new[] { 0, 99, 1 })
        {
            Assert.False(world.GetTemperament(new AgentId(id)).Success);
            Assert.False(world.SetTemperament(new AgentId(id), Axes()).Success);
        }
        Assert.Equal(AiFailure.InvalidValue, world.SetTemperament(new AgentId(10), null!).Failure);
    }

    [Theory]
    [InlineData(-1)] [InlineData(1)]
    public void BoldnessChangesOnlySpecifiedDistances(double b)
    {
        var original = new PuppetSettings(new BasicActionSettings(harassMargin: 2), keepMin: 6, keepMax: 9);
        var effective = original.ApplyTemperament(Axes(b));
        double factor = 1 - .3 * b;
        Assert.Equal(original.InterceptDistance * (1 + .3 * b), effective.InterceptDistance);
        Assert.Equal(original.InterceptStandOff * factor, effective.InterceptStandOff);
        Assert.Equal(original.HarassMargin * factor, effective.HarassMargin);
        Assert.Equal(original.KeepMin * factor, effective.KeepMin);
        Assert.Equal(original.KeepMax * factor, effective.KeepMax);
        Assert.Same(original.RoleWeights, effective.RoleWeights);
        Assert.Equal(original.TargetMinHoldTicks, effective.TargetMinHoldTicks);
        Assert.Equal(original.ReevaluationIntervalTicks, effective.ReevaluationIntervalTicks);
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(1)]
    public void PersistenceMatchesHoldAndPressureFormula(double p)
    {
        var original = new PuppetSettings(targetMinHoldTicks: 31);
        var effective = original.ApplyTemperament(Axes(p: p));
        Assert.Equal(1 + (original.TargetSwitchMargin - 1) * (1 + .5 * p), effective.TargetSwitchMargin);
        Assert.Equal((int)Math.Round(31 * (1 + .5 * p)), effective.TargetMinHoldTicks);
        foreach (int count in new[] { 0, 1, 3, 10 })
            Assert.Equal(p >= 0 ? 1 : Math.Pow(1.0 / (1 + count), -p), effective.PressureFactor(count));
        Assert.Equal(original.InterceptDistance, effective.InterceptDistance);
    }

    [Fact]
    public void ZeroKeepsExactSettingsObjectAndTuningIsConfigurable()
    {
        var original = new PuppetSettings();
        Assert.Same(original, original.ApplyTemperament(new Temperament()));
        Assert.Same(original, original.ApplyTemperament(Axes()));
        var tuned = original.WithTemperamentTuning(new TemperamentSettings(.2, .4, 2));
        var effective = tuned.ApplyTemperament(Axes(1, -1));
        Assert.Equal(original.KeepMin * .8, effective.KeepMin);
        Assert.Equal(18, effective.TargetMinHoldTicks);
        Assert.Equal(.25, effective.PressureFactor(1));
        Assert.Equal(5, original.KeepMin);
    }

    [Theory]
    [InlineData(-1)] [InlineData(1)]
    public void HarassWaitingGoalUsesScaledRing(double b)
    {
        var world = World(); var id = new AgentId(10);
        world.UpdateAction(id, ActionIds.Approach, new AgentId(1));
        var settings = new PuppetSettings().ApplyTemperament(Axes(b));
        var agent = world.Get(id).Value;
        var actions = new PuppetActions(settings, agent.Position);
        var context = new UtilityContext(world, agent, world.GetBlackboard(Side.Puppets).Value, 0);
        var resolved = actions.Resolve(context, ActionIds.Harass);
        Assert.Equal(HarassPhase.Waiting, actions.Phase);
        Assert.Equal(world.GetAttackRange(id, settings).Value + 1 * (1 - .3 * b),
            PuppetSimulation.Distance(resolved.Goal, world.Get(new AgentId(1)).Value.Position), 12);
    }

    [Theory]
    [InlineData("none")] [InlineData("tension")] [InlineData("mixed")]
    public void ExistingDistanceSettingsReproduceBoldOscillationWithoutTemperament(string stress)
    {
        var defaults = new PuppetSettings();
        var manual = new PuppetSettings(new BasicActionSettings(harassMargin: defaults.HarassMargin * .7),
            arenaHalfSize: 30, interceptDistance: defaults.InterceptDistance * 1.3,
            interceptStandOff: defaults.InterceptStandOff * .7, keepMin: defaults.KeepMin * .7,
            keepMax: defaults.KeepMax * .7);
        var existing = PuppetSimulation.Run("orbit", 1, null, manual, null, stress);
        var bold = PuppetSimulation.Run("orbit", "bold", stress: stress);
        Assert.Equal(InterceptRegressionTests.Hash(existing), InterceptRegressionTests.Hash(bold));
        Assert.Equal(existing.MaxSwitchesInWindow, bold.MaxSwitchesInWindow);
    }

    [Fact]
    public void GuardAtHealerEndHasFartherBoldInterceptGoalAndCreateSnapshotsTemperament()
    {
        var world = World();
        world.Add(new AgentState(new AgentId(13), Side.Puppets, false, TargetRole.Healer, new Vec2(0, 0), new Vec2(0, 1), .3));
        var board = world.GetBlackboard(Side.Puppets).Value;
        board.SetFact(TargetFactIds.HealerThreat, new AgentId(1));
        board.SetFact("tactic", TacticId.GuardHealer);
        var settings = new PuppetSettings(moveSpeed: 0, weightJitter: 0, reevaluationIntervalTicks: 1);
        world.SetTemperament(new AgentId(10), Axes(1));
        var bold = PuppetBrain.Create(world, settings, new DeterministicRandom(1)).Value;
        world.SetTemperament(new AgentId(10), Axes(-1));
        var cautious = PuppetBrain.Create(world, settings, new DeterministicRandom(1)).Value;
        bold.Tick(0);
        var boldGoal = bold.GetDecision(new AgentId(10)).Value;
        cautious.Tick(0);
        var cautiousGoal = cautious.GetDecision(new AgentId(10)).Value;
        Assert.Equal(ActionIds.Intercept, boldGoal.Selection.ActionId);
        Assert.Equal(ActionIds.Intercept, cautiousGoal.Selection.ActionId);
        Assert.Equal(settings.InterceptDistance * 1.3, boldGoal.Goal.Z);
        Assert.Equal(settings.InterceptDistance * .7, cautiousGoal.Goal.Z);
        Assert.True(boldGoal.Goal.Z > cautiousGoal.Goal.Z);
    }

    [Theory]
    [InlineData("bold")] [InlineData("cautious")] [InlineData("persistent")]
    [InlineData("whimsical")] [InlineData("mixed")]
    public void PresetsAreDeterministicWithoutWaver(string preset)
    {
        var first = PuppetSimulation.Run("duo-swap", preset, 821, cast: true, loadout: "mixed");
        var second = PuppetSimulation.Run("duo-swap", preset, 821, cast: true, loadout: "mixed");
        Assert.Equal(InterceptRegressionTests.Hash(first), InterceptRegressionTests.Hash(second));
        Assert.DoesNotContain(first.Frames.SelectMany(f => f.Puppets), a => a.CurrentAction == ActionIds.Waver);
    }

    [Fact]
    public void ExplicitZeroMatchesAllLegacyAndCurrentHashes()
    {
        foreach (string name in PuppetSimulation.Names)
        {
            foreach (bool legacy in new[] { false, true })
            {
                var settings = legacy ? new PuppetSettings(new BasicActionSettings(flankWeight: 0, harassWeight: 0), arenaHalfSize: 30) : null;
                Assert.Equal(InterceptRegressionTests.Hash(PuppetSimulation.Run(name, settings: settings)),
                    InterceptRegressionTests.Hash(PuppetSimulation.Run(name, "none", settings: settings)));
            }
        }
        foreach (string name in new[] { "duo", "duo-swap" })
            Assert.Equal(InterceptRegressionTests.Hash(PuppetSimulation.Run(name, 1, null, null, null, "none", true, loadout: "mixed")),
                InterceptRegressionTests.Hash(PuppetSimulation.Run(name, "none", cast: true, loadout: "mixed")));
    }

    [Theory]
    [InlineData("bold")] [InlineData("cautious")] [InlineData("persistent")]
    [InlineData("whimsical")] [InlineData("mixed")]
    public void PresetsPassUnchangedContracts27(string preset)
    {
        var limits = new PuppetScenarioSettings();
        var failures = new List<string>();
        foreach (string stress in new[] { "none", "tension", "mixed" })
            foreach (string name in PuppetSimulation.Names)
            {
                var result = PuppetSimulation.Run(name, preset, stress: stress);
                failures.AddRange(PuppetSweep.ContractViolations(result, limits).Select(v => $"{preset}/{stress}/{name}: {v}; agent={result.OscillationAgent}; " +
                    string.Join(" ", result.OscillationWindow.Select(c => $"{c.Tick}:{c.Before}>{c.After}({c.Distance:F3}m)"))));
            }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Theory]
    [InlineData("none")] [InlineData("bold")] [InlineData("cautious")]
    [InlineData("persistent")] [InlineData("whimsical")] [InlineData("mixed")]
    public void CliPrintsFixedAssignments(string preset)
    {
        var output = new StringWriter(); var error = new StringWriter();
        Assert.Equal(0, Simulation.Run(new[] { "--puppets", "duo", "--temperament", preset }, output, error));
        foreach (int id in Enumerable.Range(10, 4))
        {
            var expected = TemperamentPresets.ForAgent(preset, id);
            Assert.Contains(FormattableString.Invariant($"temperament={preset} boldness={expected[TemperamentAxisIds.Boldness]:F1} persistence={expected[TemperamentAxisIds.Persistence]:F1}"), output.ToString());
        }
        Assert.Equal("", error.ToString());
    }

    [Fact]
    public void SweepForwardsTemperamentToContracts27()
    {
        var output = new StringWriter(); var error = new StringWriter();
        Assert.Equal(0, PuppetSweep.Run(new[] { "--temperament", "mixed" }, output, error));
        Assert.Contains("temperament=mixed", output.ToString());
        var settings = new PuppetSettings(arenaHalfSize: 30);
        int failed = 0;
        foreach (string stress in new[] { "none", "tension", "mixed" })
            foreach (string name in PuppetSimulation.Names)
            {
                var result = PuppetSimulation.Run(name, "mixed", settings: settings, stress: stress);
                if (result.MaxSwitchesInWindow > 3 || result.PingPongs > 0 || result.SimultaneousRatio >= .2
                    || result.MaxInterceptors > 1 || result.MissedAttackWindows > 0) failed++;
            }
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var baseline = lines.Single(line => line.StartsWith("baseline |"));
        Assert.Equal(failed.ToString(), baseline.Split('|')[11].Trim());

        var withoutTemperament = new StringWriter();
        Assert.Equal(0, PuppetSweep.Run(Array.Empty<string>(), withoutTemperament, error));
        var defaultLines = withoutTemperament.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEqual(string.Join("\n", defaultLines),
            string.Join("\n", lines.Where(line => !line.StartsWith("temperament="))));
        Assert.Equal("", error.ToString());
    }

    [Theory]
    [InlineData("--temperament", "unknown")]
    [InlineData("--temperament", "bold", "--temperament", "mixed")]
    [InlineData("--temperament")]
    public void SweepRejectsInvalidTemperamentArguments(params string[] args)
    {
        Assert.Equal(2, PuppetSweep.Run(args, new StringWriter(), new StringWriter()));
    }
}
