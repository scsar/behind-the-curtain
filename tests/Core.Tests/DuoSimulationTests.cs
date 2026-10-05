using System;
using System.Linq;
using System.IO;
using BehindTheCurtain.Core;
using BehindTheCurtain.Core.Sim;
using Xunit;
namespace BehindTheCurtain.Core.Tests;
public class DuoSimulationTests
{
    [Theory]
    [InlineData("approach", "5B74BC8F308BB5E7A9904761CADDE9C703B98546FDE0C3F34151469193FF8873")]
    [InlineData("circle", "CB1C0FCBA86C8DF45EE43895B30724271A392963F657022524BCB4ED8745F7F4")]
    [InlineData("retreat", "86E8159A2FFE3E36C2AE41D780AAEA5C3751B82F7A859310B87B8E7C5D491B2A")]
    [InlineData("healer", "07B77854DC5E970DC1E4009BA279D93C4531ADF450AA6CAE27A8623B7DBF8D48")]
    [InlineData("standstill", "9F9124C65F2F4545AEC443B852245CC1DF4F53E43AE88D87518795604FDD2A66")]
    [InlineData("orbit", "19604E9C25D85604FBDD43DE2DBDFB26A1A370F87A907C0C9C77B904C6B612CB")]
    [InlineData("passby", "3551FFDAEA224D4D39BCC32012B6DA85DF726E95255D0277CF474E520FD40443")]
    public void LegacySevenTrajectoriesRemainUnchanged(string name, string expected)
    {
        var r = PuppetSimulation.Run(name, settings: new PuppetSettings(new BasicActionSettings(flankWeight: 0, harassWeight: 0), arenaHalfSize: 30));
        Assert.Equal(expected, InterceptRegressionTests.Hash(r));
        Assert.All(r.Frames, f => Assert.Equal(1, Assert.Single(f.Players).Id.Value));
        using var output = new StringWriter(); PuppetSimulation.Print(r, output);
        Assert.Contains("targetDistribution", output.ToString());
    }

    [Theory]
    [InlineData("duo")]
    [InlineData("duo-swap")]
    public void DuoPathsThreatAndDistributionAreCorrect(string name)
    {
        var s = new PuppetScenarioSettings(); var settings = new PuppetSettings();
        var r = PuppetSimulation.Run(name);
        Assert.True(r.HasHealerThreat);
        foreach (var f in r.Frames)
        {
            Assert.Equal(new[] { 1, 2 }, f.Players.Select(p => p.Id.Value));
            Assert.All(f.Players, p => { Assert.True(p.IsHuman); Assert.Equal(Side.Players, p.Side); });
            var a = f.Players[0].Position; var b = f.Players[1].Position;
            int swap = s.DuoSwapSecond * settings.TicksPerSecond;
            Vec2 Expected(Vec2 start, Vec2 end, int ticks)
            {
                double fraction = Math.Min(1, s.DuoSpeed * ticks / settings.TicksPerSecond / PuppetSimulation.Distance(start, end));
                return new Vec2(start.X + (end.X-start.X)*fraction, start.Z + (end.Z-start.Z)*fraction);
            }
            var expectedA = name == "duo-swap" && f.Tick >= swap ? Expected(s.DuoFrontStop, s.DuoSideStart, f.Tick-swap) : Expected(s.DuoFrontStart,s.DuoFrontStop,f.Tick);
            var expectedB = name == "duo-swap" && f.Tick >= swap ? Expected(s.DuoSideStart,s.DuoFrontStop,f.Tick-swap) : s.DuoSideStart;
            Assert.Equal(expectedA.X,a.X,10); Assert.Equal(expectedA.Z,a.Z,10);
            Assert.Equal(expectedB.X,b.X,10); Assert.Equal(expectedB.Z,b.Z,10);
            Assert.All(f.Puppets.Where(p => p.CurrentAction == ActionIds.Intercept), p => Assert.Equal(2,p.Target!.Value.Value));
        }
        Assert.Equal(r.InterceptAgentTicks,r.InterceptTargetTicks[2]); Assert.Equal(0,r.InterceptTargetTicks[1]);
        Assert.True(r.InterceptAgentTicks > 0);
        int count = r.Frames.Sum(f => f.Puppets.Count(a => a.Target.HasValue));
        Assert.Equal(count,r.TargetedAgentTicks); Assert.Equal(count,r.TargetAgentTicks.Values.Sum());
        foreach (int id in new[] {1,2}) Assert.Equal(r.Frames.Sum(f => f.Puppets.Count(a => a.Target?.Value == id)),r.TargetAgentTicks[id]);
        int eligible = r.Frames.Count(f => f.Puppets.Any(a => a.Role != TargetRole.Healer && a.Target.HasValue));
        int concentrated = r.Frames.Count(f => { var a = f.Puppets.Where(a => a.Role != TargetRole.Healer).ToArray(); return a.All(p => p.Target.HasValue) && a.Select(p => p.Target).Distinct().Count() == 1; });
        Assert.Equal(eligible,r.NonHealerTargetTicks); Assert.Equal(concentrated,r.ConcentratedTicks);
    }

    [Fact]
    public void SwapInterceptorFollowsBNewSegmentWithinTwoSeconds()
    {
        var s = new PuppetScenarioSettings(); var settings = new PuppetSettings();
        var r = PuppetSimulation.Run("duo-swap");
        int swap = s.DuoSwapSecond * settings.TicksPerSecond;
        int deadline = swap + s.DuoRecoverySeconds * settings.TicksPerSecond;
        var initial = r.Frames[swap].Puppets.Single(a => a.CurrentAction == ActionIds.Intercept);
        var final = r.Frames[deadline];
        var interceptor = final.Puppets.Single(a => a.CurrentAction == ActionIds.Intercept);
        Assert.Equal(initial.Id,interceptor.Id); Assert.Equal(2,interceptor.Target!.Value.Value);
        var healer = final.Puppets.Single(a => a.Role == TargetRole.Healer);
        var target = final.Players.Single(a => a.Id.Value == 2);
        Assert.True(PuppetSimulation.Distance(r.Frames.Last().Players[1].Position,s.DuoFrontStop) < 1e-8);
        Assert.InRange(PuppetSimulation.Distance(interceptor.Position,InterceptGeometry.ClosestPoint(interceptor.Position,healer.Position,s.DuoFrontStop,settings)),0,s.InterceptQualityDistance);
        Assert.True(PuppetSimulation.Distance(initial.Position,interceptor.Position) > 1);
        Assert.InRange(PuppetSimulation.Distance(interceptor.Position,InterceptGeometry.ClosestPoint(interceptor.Position,healer.Position,target.Position,settings)),0,s.InterceptQualityDistance);
        Assert.All(r.Frames.Skip(deadline), f => {
            var a = f.Puppets.Single(a => a.CurrentAction == ActionIds.Intercept);
            Assert.InRange(PuppetSimulation.Distance(a.Position,InterceptGeometry.ClosestPoint(a.Position,f.Puppets.Single(p => p.Role == TargetRole.Healer).Position,f.Players[1].Position,settings)),0,s.InterceptQualityDistance);
        });
    }

    [Theory]
    [InlineData("duo")]
    [InlineData("duo-swap")]
    public void DuoForbiddenPatternsJitterAndDeterminismPass(string name)
    {
        var r = PuppetSimulation.Run(name); var s = new PuppetScenarioSettings();
        Assert.Equal(0,r.FleeTicks); Assert.Equal(0,r.InactivityFailures); Assert.Equal(0,r.PingPongs);
        Assert.Equal(0,r.MissedAttackWindows); Assert.Equal(0,r.JitterFailures);
        Assert.InRange(r.MaxInterceptors,0,1); Assert.True(r.MaxSwitchesInWindow <= s.SwitchLimit);
        Assert.True(r.SimultaneousRatio < s.SynchronizationLimit);
        Assert.Equal(InterceptRegressionTests.Hash(r),InterceptRegressionTests.Hash(PuppetSimulation.Run(name)));
        string Print(PuppetTrajectory trajectory) { using var w = new StringWriter(); PuppetSimulation.Print(trajectory,w); return w.ToString(); }
        Assert.Equal(Print(r),Print(PuppetSimulation.Run(name)));
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.Equal(0,Simulation.Run(new[] {"--puppets",name},output,error)); Assert.Equal("",error.ToString());
        Assert.Contains("targetDistribution",output.ToString()); Assert.Contains("interceptQuality",output.ToString()); Assert.Contains("jitter=0",output.ToString());
    }
}
