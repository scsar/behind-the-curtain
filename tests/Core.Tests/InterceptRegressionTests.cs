using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BehindTheCurtain.Core.Sim;
using Xunit;
namespace BehindTheCurtain.Core.Tests;
public class InterceptRegressionTests
{
    internal static string Hash(PuppetTrajectory r) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", r.Frames.SelectMany(f => f.Puppets.Select(a => FormattableString.Invariant($"{f.Tick}:{a.Id.Value}:{a.Position.X:R}:{a.Position.Z:R}:{a.CurrentAction}:{a.Target}")))))));
    [Theory]
    [InlineData("approach", "D962516E72898E5FA7A035634AF88A171C69455757DE161924E468730A064C79")]
    [InlineData("circle", "F9B1A5F0E6522E61EDC39A701665B1164939B0C5548CCB5B406C0ECD72EE26A5")]
    [InlineData("retreat", "178D2A898746D4E410344F2E4FB1C13B7D9F333FD6D8F8A424B01BB89E45E2E3")]
    public void UnchangedNonInterceptTrajectory(string name, string expected) => Assert.Equal(expected, Hash(PuppetSimulation.Run(name)));

    [Fact]
    public void StandstillSettlesWithoutPositionJitter()
    {
        var r = PuppetSimulation.Run("standstill");
        foreach (int id in new[] { 10, 11, 12, 13 })
        {
            var points = r.Frames.Select(f => f.Puppets.Single(a => a.Id.Value == id).Position).ToArray();
            var reversals = Enumerable.Range(151, 150).Where(t =>
            {
                var p = points[t-2]; var q = points[t-1]; var v = points[t];
                return PuppetSimulation.Distance(p,q) > .001 && PuppetSimulation.Distance(q,v) > .001
                    && (q.X-p.X)*(v.X-q.X)+(q.Z-p.Z)*(v.Z-q.Z) < 0;
            }).ToArray();
            Assert.All(Enumerable.Range(180,121), t => Assert.True(reversals.Count(x => x > t-30 && x <= t) <= 3));
            if (id == 13 || r.Frames.Last().Puppets.Single(a => a.Id.Value == id).CurrentAction == ActionIds.Intercept)
                Assert.True(Enumerable.Range(211,90).Sum(t => PuppetSimulation.Distance(points[t-1],points[t])) <= .2);
        }
        Assert.True(r.InterceptAgentTicks > 0);
    }

    [Fact]
    public void OrbitRequiresInterceptorToActuallyFollow()
    {
        var r = PuppetSimulation.Run("orbit");
        Assert.True(r.InterceptAgentTicks > 0);
        Assert.True(r.InterceptQualityRatio >= .8, $"quality={r.InterceptQualityRatio}");
        Assert.True(r.InterceptTravel >= 3, $"travel={r.InterceptTravel}");
        Assert.All(r.Frames, f => Assert.Equal(new Vec2(0,-3), f.Puppets.Single(a => a.Id.Value == 13).Position));
    }

    [Fact]
    public void MovingHealerThreatDoesNotPinInterceptorForASecond()
    {
        var r = PuppetSimulation.Run("healer");
        foreach (int id in new[] {10,11})
            for (int t = 30; t < 188; t++)
            {
                var window = r.Frames.Skip(t-30).Take(31).Select(f => f.Puppets.Single(a => a.Id.Value == id)).ToArray();
                if (window.All(a => a.CurrentAction == ActionIds.Intercept))
                    Assert.True(window.Any(a => PuppetSimulation.Distance(a.Position, window[0].Position) > .01), $"id={id} tick={t}");
            }
    }

    [Theory]
    [InlineData(.29, true)] [InlineData(.31, false)]
    public void InterceptHoldToleranceBoundary(double offset, bool holds)
    {
        var (w,b) = Setup(new Vec2(offset,3), new Vec2(0,10), new Vec2(0,0));
        b.Tick(0);
        Assert.Equal(holds ? new Vec2(offset,3) : new Vec2(0,3), b.GetDecision(new AgentId(10)).Value.Goal);
    }

    [Theory]
    [InlineData(6,6)] [InlineData(4,5)] [InlineData(9,8)]
    public void ReservedHealerUsesRadialBand(double distance, double expected)
    {
        var (w,b) = Setup(new Vec2(2,-3), new Vec2(0,0), new Vec2(0,-distance));
        w.GetBlackboard(Side.Puppets).Value.SetIntention("Healer:keepDistance", 10);
        w.GetBlackboard(Side.Puppets).Value.Reserve("intercept:13",new AgentId(10),10,0);
        b.Tick(0);
        Assert.Equal(new Vec2(0,-expected), b.GetDecision(new AgentId(13)).Value.Goal);
    }

    [Theory]
    [InlineData(6,true)] [InlineData(4,false)] [InlineData(5,false)]
    public void PassedInterceptorRetainsSuitabilityInAttackRange(double guardZ, bool eligible)
    {
        var (w,b) = Setup(new Vec2(0,guardZ), new Vec2(0,5),new Vec2(0,0));
        b.Tick(0);
        Assert.Equal(eligible, b.GetDecision(new AgentId(10)).Value.Selection.Scores.Single(s => s.ActionId == ActionIds.Intercept).Score > 0);
    }

    [Fact]
    public void PassbyRecoversWithoutCrossingPlayer()
    {
        var r = PuppetSimulation.Run("passby");
        int crossed = r.Frames.First(f => f.Player.Z < 0).Tick;
        double Gap(PuppetFrame f) {
            var a = f.Puppets.Single(a => a.Id.Value == 10); var h = f.Puppets.Single(a => a.Id.Value == 13);
            return PuppetSimulation.Distance(a.Position,InterceptGeometry.ClosestPoint(a.Position,h.Position,f.Player,new PuppetSettings()));
        }
        int departed = r.Frames.First(f => f.Tick >= crossed && Gap(f) > 1).Tick;
        int recovered = r.Frames.First(f => f.Tick > departed && Gap(f) <= 1).Tick;
        Assert.True(recovered <= crossed+60, $"crossed={crossed} recovered={recovered}");
        var rest = r.Frames.Where(f => f.Tick >= recovered).ToArray();
        Assert.True(rest.Count(f => Gap(f)<=1)/(double)rest.Length >= .8, $"recovered={recovered} ratio={rest.Count(f => Gap(f)<=1)/(double)rest.Length}");
        Assert.All(r.Frames, f =>
            Assert.True(PuppetSimulation.Distance(f.Puppets.Single(a => a.Id.Value == 10).Position,f.Player) >= .5, $"tick={f.Tick}"));
    }

    [Fact]
    public void AllScenariosAndLoadoutsPassForbiddenPatternsAndAreDeterministic()
    {
        foreach (string name in new[] { "approach", "circle", "retreat", "healer", "standstill", "orbit", "passby" })
        foreach (string loadout in new[] {"presets","mixed","noprotect"})
        {
            var r = PuppetSimulation.Run(name,1,null,null,null,"none",loadout == "mixed",loadout:loadout);
            Assert.Equal(0,r.JitterFailures);
            Assert.Equal(0,r.FleeTicks); Assert.Equal(0,r.InactivityFailures); Assert.Equal(0,r.PingPongs);
            Assert.Equal(0,r.MissedAttackWindows); Assert.InRange(r.MaxInterceptors,0,1);
            Assert.True(r.MaxSwitchesInWindow <= 3,$"{name}/{loadout}: switches={r.MaxSwitchesInWindow}");
            Assert.True(r.SimultaneousRatio < .2,$"{name}/{loadout}: simultaneous={r.SimultaneousRatio}");
            Assert.Equal(Hash(r),Hash(PuppetSimulation.Run(name,1,null,null,null,"none",loadout == "mixed",loadout:loadout)));
        }
    }


    [Theory]
    [InlineData(0, 0, 1.5)] [InlineData(12, 10, 9)] [InlineData(0, 1, 1.5)]
    public void SegmentProjectionAndShortSegment(double positionZ, double threatZ, double expectedZ)
    {
        var point = InterceptGeometry.ClosestPoint(new Vec2(3,positionZ),new Vec2(0,0),new Vec2(0,threatZ),new PuppetSettings());
        Assert.Equal(new Vec2(0,expectedZ),point);
    }

    [Fact]
    public void DetourChoosesNearSideAndResumesOriginalGoal()
    {
        var (w,b) = Setup(new Vec2(.2,6),new Vec2(0,5),new Vec2(0,0));
        b.Tick(0);
        var first = b.GetDecision(new AgentId(10)).Value.Goal;
        Assert.InRange(PuppetSimulation.Distance(first,new Vec2(0,5)),1-1e-10,1+1e-10);
        Assert.True(first.X > 0);
        w.UpdatePosition(new AgentId(10),first,new Vec2(0,-1));
        b.Tick(1);
        var next = b.GetDecision(new AgentId(10)).Value.Goal;
        Assert.Equal(InterceptGeometry.ClosestPoint(first,new Vec2(0,0),new Vec2(0,5),new PuppetSettings()),next);
    }

    [Fact]
    public void InterceptMayMoveAwayFromNearbyHuman()
    {
        var (w,unused) = Setup(new Vec2(.2,6),new Vec2(0,5),new Vec2(0,0));
        var b = PuppetBrain.Create(w,new PuppetSettings(),new DeterministicRandom(1)).Value;
        bool movedAway = false;
        for (int t=0;t<30;t++)
        {
            var before = w.Get(new AgentId(10)).Value;
            b.Tick(t);
            var after = w.Get(new AgentId(10)).Value;
            if (after.CurrentAction == ActionIds.Intercept && PuppetSimulation.Distance(after.Position,new Vec2(0,5)) > PuppetSimulation.Distance(before.Position,new Vec2(0,5))) movedAway = true;
        }
        Assert.True(movedAway);
    }

    [Fact]
    public void NoInterceptPrintsNotApplicable()
    {
        var r = PuppetSimulation.Run("healer",1,null,null,null,"none",loadout:"noprotect");
        var output = new System.IO.StringWriter(); PuppetSimulation.Print(r,output);
        Assert.Contains("interceptQuality ticks=0 n/a(",output.ToString());
        Assert.Equal(0,r.InterceptSettledTicks);
    }


    [Fact]
    public void PositionJitterDetectsReversalsWithinSameAction()
    {
        var r = PuppetSimulation.Run("orbit", scenario: new PuppetScenarioSettings { AngularSpeed = Math.PI * 30 });
        Assert.True(r.JitterFailures > 0);
        Assert.True(r.MaxDirectionReversals.Values.Max() > 3);
        Assert.Contains(Enumerable.Range(2,r.Frames.Count-2), t =>
        {
            var a = r.Frames[t-2].Puppets.Single(x => x.Id.Value == 10);
            var b = r.Frames[t-1].Puppets.Single(x => x.Id.Value == 10);
            var c = r.Frames[t].Puppets.Single(x => x.Id.Value == 10);
            return a.CurrentAction == b.CurrentAction && b.CurrentAction == c.CurrentAction
                && PuppetSimulation.Distance(a.Position,b.Position) > .001 && PuppetSimulation.Distance(b.Position,c.Position) > .001
                && (b.Position.X-a.Position.X)*(c.Position.X-b.Position.X)+(b.Position.Z-a.Position.Z)*(c.Position.Z-b.Position.Z) < 0;
        });
        var relaxed = PuppetSimulation.Run("orbit", scenario: new PuppetScenarioSettings { AngularSpeed = Math.PI * 30, JitterMaxReversals = 30 });
        Assert.Equal(0,relaxed.JitterFailures);
        Assert.Equal(r.MaxDirectionReversals.OrderBy(x=>x.Key),relaxed.MaxDirectionReversals.OrderBy(x=>x.Key));
    }


    [Fact]
    public void InvalidInterceptSettingsAreRejected()
    {
        foreach (var settings in new[] {
            new PuppetSettings(interceptStandOff:0), new PuppetSettings(interceptStandOff:double.NaN),
            new PuppetSettings(interceptHoldTolerance:-.1), new PuppetSettings(interceptHoldTolerance:double.PositiveInfinity),
            new PuppetSettings(interceptClearance:0), new PuppetSettings(interceptClearance:double.NaN),
            new PuppetSettings(interceptWaypointTolerance:0) })
            Assert.Equal(AiFailure.InvalidSettings,PuppetBrain.Create(new World(),settings,new DeterministicRandom(1)).Failure);
    }

    private static (World,PuppetBrain) Setup(Vec2 guard, Vec2 human, Vec2 healer)
    {
        var w = new World();
        w.Add(new AgentState(new AgentId(1),Side.Players,true,TargetRole.Attacker,human,new Vec2(0,1),.3));
        w.Add(new AgentState(new AgentId(10),Side.Puppets,false,TargetRole.Guard,guard,new Vec2(0,1),.3));
        w.Add(new AgentState(new AgentId(13),Side.Puppets,false,TargetRole.Healer,healer,new Vec2(0,1),.3));
        var board = w.GetBlackboard(Side.Puppets).Value;
        board.SetFact(TargetFactIds.HealerThreat,new AgentId(1)); board.SetFact("tactic",TacticId.GuardHealer);
        return (w,PuppetBrain.Create(w,new PuppetSettings(moveSpeed:0,weightJitter:0),new DeterministicRandom(1)).Value);
    }
}
