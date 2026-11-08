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
    [InlineData("approach", "990E15B8FC2F4276869B52F58BF480B12F7314C901B2440476E76CEE98458EF8")]
    [InlineData("circle", "92E2CD303400282F886BD346A990F1A97CBA68271841101D1217C805ADA42114")]
    [InlineData("retreat", "55F0F0A75C67763A5115F6B4DEA248C3B8689EFD388B3C231B537ED473D13A96")]
    [InlineData("healer", "A691953E29CCF87B3B164726714C7189E10A8CA042AED81DE4906EC02F89AFC4")]
    [InlineData("standstill", "5C7393D4C694521D052FFDFAB9687262ABA0C4D1533908514B812B630A8E5ABC")]
    [InlineData("orbit", "5726F2BAF0AB88268CD7761C270094075CCEC477CBF4F931919FFF10A2E9331A")]
    [InlineData("passby", "8E435F28A1D1374CC211D08A247AAC38FEB7F021B4F0B9835A8E59A0105C115D")]
    public void LegacySevenTrajectoriesHaveReviewedHashes(string name, string expected)
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
        Assert.Equal(0,r.MissedAttackWindows); Assert.Equal(0,r.JitterFailures); Assert.Equal(0, r.SurroundReservationConflicts);
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
