using System;
using System.IO;
using System.Linq;
using BehindTheCurtain.Core.Sim;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public class OscillationTests
{
    [Theory]
    [InlineData("approach", "attack", 2, 9, true)]
    [InlineData("approach", "attack", 2.01, 1, false)]
    [InlineData("attack", "approach", 1, 2.01, true)]
    [InlineData("attack", "approach", 9, 2, false)]
    [InlineData("hold", "approach", 1, 9, false)]
    [InlineData("attack", "attack", 1, 9, false)]
    public void BoundaryUsesNewTargetOnEntryAndPreviousTargetOnExit(
        string before, string after, double nextDistance, double previousDistance, bool expected)
    {
        Assert.Equal(expected, PuppetSimulation.IsAttackRangeBoundary(before, after, nextDistance, previousDistance, 2));
    }

    [Fact]
    public void OscillationWindowExcludesBoundariesAndKeepsStrictSlidingWindow()
    {
        var r = new PuppetTrajectory();
        for (int tick = 10; tick <= 50; tick += 10)
            PuppetSimulation.RecordOscillationSwitch(r, 11, new(tick, "attack", "approach", 3), true, 90);
        Assert.Equal(0, r.MaxSwitchesInWindow);
        Assert.Empty(r.OscillationSwitches);
        foreach (int tick in new[] { 60, 70, 80, 90 })
            PuppetSimulation.RecordOscillationSwitch(r, 11, new(tick, "intercept", "waver", 1.4), false, 90);
        Assert.Equal(4, r.MaxSwitchesInWindow);
        Assert.Equal(11, r.OscillationAgent);
        Assert.Equal(new[] { 60, 70, 80, 90 }, r.OscillationWindow.Select(c => c.Tick));
        PuppetSimulation.RecordOscillationSwitch(r, 11, new(150, "waver", "hold", 1), false, 90);
        Assert.Equal(4, r.MaxSwitchesInWindow); // tick 60 is outside (60,150].
        PuppetSimulation.RecordOscillationSwitch(r, 11, new(151, "hold", "waver", 1), false, 90);
        Assert.Equal(5, r.MaxSwitchesInWindow);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void Contracts27MatchesIndependentRunsAndPrintedFailureCount(int interval)
    {
        var settings = new PuppetSettings(arenaHalfSize: 30, reevaluationIntervalTicks: interval);
        int failed = 0;
        foreach (string stress in new[] { "none", "tension", "mixed" })
            foreach (string name in PuppetSimulation.Names)
            {
                var r = PuppetSimulation.Run(name, 1, null, settings, null, stress);
                if (r.MaxSwitchesInWindow > 3 || r.PingPongs > 0 || r.SimultaneousRatio >= .2
                    || r.MaxInterceptors > 1 || r.MissedAttackWindows > 0) failed++;
            }
        if (interval == new PuppetSettings().ReevaluationIntervalTicks) Assert.Equal(0, failed);
        using var output = new StringWriter();
        Assert.Equal(0, PuppetSweep.Run(new[] { "PuppetSettings.reevaluationIntervalTicks=" + interval }, output, new StringWriter()));
        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var row = lines.Single(l => l.StartsWith("1 |"));
        Assert.Equal(failed.ToString(), row.Split('|')[11].Trim());
        Assert.Equal(failed, lines.Count(l => l.StartsWith("rank 1 27조합:")));
        Assert.Equal(failed, lines.Count(l => l.StartsWith("rank 1 진동 ")));
    }
}
