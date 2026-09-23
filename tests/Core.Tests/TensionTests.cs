using System;
using System.Linq;
using System.Collections.Generic;
using BehindTheCurtain.Core.Sim;
using Xunit;
namespace BehindTheCurtain.Core.Tests;
public sealed class TensionTests
{
    static World WorldWith(int count = 3, PuppetKind kind = PuppetKind.Puppet)
    {
        var w = new World();
        for (int i = 1; i <= count; i++)
            w.Add(new AgentState(new AgentId(i), Side.Puppets, false, TargetRole.Attacker, new Vec2(i, 0), new Vec2(0, 1), .3), new PuppetInitialState(kind));
        w.Add(new AgentState(new AgentId(100), Side.Players, true, TargetRole.Attacker, new Vec2(20, 0), new Vec2(0, 1), .3));
        return w;
    }
    static PuppetBrain Brain(World w, DirectorProfile? p = null) => PuppetBrain.Create(w,
        new PuppetSettings(moveSpeed: 0, weightJitter: 0, persistenceBonus: 0, reevaluationIntervalTicks: 1), new DeterministicRandom(1), p ?? new DirectorProfile()).Value;
    [Theory]
    [InlineData("approach")][InlineData("circle")][InlineData("retreat")][InlineData("healer")]
    public void FullTensionRegression(string name)
    {
        var a = PuppetSimulation.Run(name);
        var b = PuppetSimulation.Run(name, 1, null, null, null, "none", new DirectorProfile(tuneBudgetPerSecond: 0, enableWaver: false));
        Assert.Equal(a.Frames.SelectMany(f => f.Puppets).Select(a => (a.Position, a.CurrentAction)), b.Frames.SelectMany(f => f.Puppets).Select(a => (a.Position, a.CurrentAction)));
    }
    [Fact]
    public void ScoresCauseAndStationaryWaver()
    {
        var w = WorldWith(1); var b = Brain(w, new DirectorProfile(tuneBudgetPerSecond: 0));
        w.ReduceTension(new AgentId(1), .5, 0); b.Tick(0);
        var d = b.GetDecision(new AgentId(1)).Value;
        Assert.Equal(ActionIds.Waver, d.Selection.ActionId); Assert.Null(d.Target); Assert.Equal(new Vec2(1, 0), d.Goal);
        Assert.Equal(PuppetInputKind.ReduceTension, d.Cause!.StateEvent!.Cause.Kind);
        foreach (var s in d.Selection.Scores)
        {
            Assert.Equal(.5 * s.Product * s.BaseWeight * s.IntentionMultiplier, s.RoleShare, 12);
            Assert.Equal(s.ActionId == ActionIds.Waver ? .5 : 0, s.PullShare);
            Assert.Equal(s.RoleShare + s.PullShare, s.Score);
        }
    }
    [Fact]
    public void WaverShareGrowsAsTensionFalls()
    {
        var ratios = new List<double>();
        foreach (double t in new[] { .9, .5, .1 })
        {
            var w = WorldWith(1); var b = Brain(w, new DirectorProfile(tuneBudgetPerSecond: 0));
            w.GetBlackboard(Side.Puppets).Value.SetIntention("Attacker:attack", 2);
            w.ReduceTension(new AgentId(1), 1-t, 0); int wavering = 0;
            for (int tick=0; tick<90; tick++)
            {
                w.UpdatePosition(new AgentId(100), new Vec2(tick % 2 == 0 ? 2 : 20, 0), new Vec2(0,1));
                b.Tick(tick); if (w.Get(new AgentId(1)).Value.CurrentAction == ActionIds.Waver) wavering++;
            }
            ratios.Add(wavering / 90.0);
        }
        Assert.True(ratios[0] < ratios[1] && ratios[1] < ratios[2]);
    }
    [Theory]
    [InlineData(TunePriority.OutOfPosition, 2)][InlineData(TunePriority.LowestTension, 3)][InlineData(TunePriority.Attacking, 2)]
    public void PrioritiesAndSingleBudget(TunePriority priority, int expected)
    {
        var w = WorldWith(); var b = Brain(w, new DirectorProfile(priority));
        for (int i=1;i<=3;i++) w.ReduceTension(new AgentId(i), i == 3 ? .9 : .2, 0);
        w.UpdatePosition(new AgentId(2), new Vec2(19,0), new Vec2(0,1));
        var before = w.GetPuppetStates().ToArray(); b.Tick(0);
        Assert.Equal(expected, b.TuneTarget!.Value.Value);
        Assert.InRange(b.TuneAmount, 0, .2/30);
        Assert.Single(w.GetPuppetStates(), s => s.Tension > before.Single(x=>x.Id.Equals(s.Id)).Tension);
    }
    [Theory]
    [InlineData(TunePriority.OutOfPosition)][InlineData(TunePriority.LowestTension)][InlineData(TunePriority.Attacking)]
    public void TiesUseAgentId(TunePriority priority)
    {
        var w = WorldWith(); var b = Brain(w, new DirectorProfile(priority));
        foreach(var s in w.GetPuppetStates()) w.ReduceTension(s.Id,.4,0);
        b.Tick(0); Assert.Equal(1,b.TuneTarget!.Value.Value);
    }
    [Theory]
    [InlineData(PuppetKind.Puppet, true, .3, PuppetStatus.Broken)]
    [InlineData(PuppetKind.Humanized, true, .25, PuppetStatus.Downed)]
    [InlineData(PuppetKind.Puppet, false, .2, PuppetStatus.Released)]
    [InlineData(PuppetKind.Humanized, false, .2, PuppetStatus.Groggy)]
    public void ReclaimTransitions(PuppetKind kind, bool damage, double budget, PuppetStatus status)
    {
        var w = WorldWith(2,kind); var b=Brain(w);
        w.ReduceTension(new AgentId(2),.8,0);
        if(damage) w.ApplyDamage(new AgentId(1),100,0,0); else w.ReduceTension(new AgentId(1),1,0);
        b.Tick(0); Assert.Equal(status,w.GetPuppetState(new AgentId(1)).Value.Status);
        Assert.Equal(budget,b.CurrentTuneBudgetPerSecond,12); Assert.Equal(budget/30,b.TuneAmount,12);
        Assert.Equal(damage?1:0,w.StateEvents.Count(e=>e.Kind==PuppetStateEventKind.StringReclaimed));
        b.Tick(1); Assert.Equal(budget,b.CurrentTuneBudgetPerSecond,12);
    }
    [Fact]
    public void ReclaimCapsExactlyAndSpeedsRecovery()
    {
        var w=WorldWith(5);var b=Brain(w);
        w.ReduceTension(new AgentId(5),.9,0); b.Tick(0);double slow=b.TuneAmount;
        for(int i=1;i<=4;i++)w.ApplyDamage(new AgentId(i),100,0,1);
        b.Tick(1);Assert.Equal(.4,b.CurrentTuneBudgetPerSecond);Assert.Equal(2*slow,b.TuneAmount,12);
        Assert.Equal(4,w.StateEvents.Count(e=>e.Kind==PuppetStateEventKind.StringReclaimed));
    }
    [Fact]
    public void ZeroRoleActionWithoutStateChangeExplainsIntention()
    {
        var w = WorldWith(1);
        w.GetBlackboard(Side.Puppets).Value.SetIntention("Attacker:keepDistance", 10);
        var b = Brain(w); b.Tick(0);
        var d = b.GetDecision(new AgentId(1)).Value;
        Assert.Equal(ActionIds.KeepDistance, d.Selection.ActionId);
        Assert.NotNull(d.Cause); Assert.Null(d.Cause.StateEvent);
        Assert.Equal("Attacker:keepDistance", d.Cause.IntentionKey); Assert.Equal(10, d.Cause.Intention);
    }
    [Fact]
    public void TuningClampsAndInvalidTicksDoNotSpendBudget()
    {
        var w = WorldWith(1); var b = Brain(w);
        w.ReduceTension(new AgentId(1), .001, 0); b.Tick(0);
        Assert.Equal(1, w.GetPuppetState(new AgentId(1)).Value.Tension);
        Assert.Equal(.001, b.TuneAmount, 12);
        int count = w.StateEvents.Count;
        Assert.Equal(AiFailure.InvalidTick, b.Tick(0)); Assert.Equal(count, w.StateEvents.Count);
        b.Tick(1); Assert.Null(b.TuneTarget); Assert.Equal(0,b.TuneAmount);
    }
    [Fact]
    public void InvalidProfilesAreRejectedAndCliValidatesStress()
    {
        foreach (var p in new[] { new DirectorProfile(tuneBudgetPerSecond: -1), new DirectorProfile(waverPull: double.NaN),
            new DirectorProfile(humanizedReclaimRatio: 2), new DirectorProfile(tuneBudgetMaxScale: .5), new DirectorProfile((TunePriority)99) })
            Assert.Equal(AiFailure.InvalidSettings, PuppetBrain.Create(WorldWith(),new PuppetSettings(),new DeterministicRandom(1),p).Failure);
        foreach (var args in new[] { new[] { "--stress", "mixed" }, new[] { "--puppets", "circle", "--stress", "invalid" } })
            Assert.Equal(2, Simulation.Run(args,new System.IO.StringWriter(),new System.IO.StringWriter()));
    }
    [Theory]
    [InlineData("none")][InlineData("tension")][InlineData("mixed")]
    public void StressContractsAndDeterminism(string stress)
    {
        foreach(string name in PuppetSimulation.Names)
        {
            var a=PuppetSimulation.Run(name,1,null,null,null,stress);
            var b=PuppetSimulation.Run(name,1,null,null,null,stress);
            Assert.Equal(a.Frames.SelectMany(f=>f.Puppets).Select(x=>(x.Position,x.CurrentAction)),b.Frames.SelectMany(f=>f.Puppets).Select(x=>(x.Position,x.CurrentAction)));
            Assert.Equal(a.Frames.Select(f=>(f.Budget,f.TuneTarget,f.TuneAmount)), b.Frames.Select(f=>(f.Budget,f.TuneTarget,f.TuneAmount)));
            Assert.Equal(a.Frames.SelectMany(f=>f.States).Select(s=>(s.Tension,s.Posture,s.Status)),b.Frames.SelectMany(f=>f.States).Select(s=>(s.Tension,s.Posture,s.Status)));
            Assert.True(a.MaxSwitchesInWindow<=3,$"{stress}/{name}: switches {a.MaxSwitchesInWindow}");
            Assert.Equal(0,a.PingPongs);Assert.True(a.SimultaneousRatio<.2,$"{stress}/{name}: sync {a.SimultaneousRatio}");
            Assert.InRange(a.MaxInterceptors,0,1);Assert.Equal(0,a.MissedAttackWindows);
        }
    }
}
