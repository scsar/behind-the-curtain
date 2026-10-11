using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BehindTheCurtain.Core.Sim;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public class SweepTests
{
    [Fact]
    public void BuilderDefaultsSingleOverrideAndUnknownName()
    {
        var scenario = new PuppetScenarioSettings();
        Assert.True(PuppetTuningBuilder.TryBuild(new Dictionary<string,string>(), scenario, out var built, out var differences, out _));
        var original = new PuppetSettings(arenaHalfSize: scenario.ArenaHalfSize);
        foreach (var p in typeof(PuppetSettings).GetProperties().Where(p => p.PropertyType == typeof(double) || p.PropertyType == typeof(int)))
            Assert.Equal(p.GetValue(original), p.GetValue(built));
        Assert.Equal("", differences);
        Assert.True(PuppetTuningBuilder.TryBuild(new Dictionary<string,string> { ["BasicActionSettings.harassIntervalTicks"] = "45" }, scenario, out built, out differences, out _));
        foreach (var p in typeof(PuppetSettings).GetProperties().Where(p => p.PropertyType == typeof(double) || p.PropertyType == typeof(int)))
            Assert.Equal(p.Name == "HarassIntervalTicks" ? 45 : p.GetValue(original), p.GetValue(built));
        Assert.Equal("BasicActionSettings.harassIntervalTicks=45", differences);
        Assert.False(PuppetTuningBuilder.TryBuild(new Dictionary<string,string> { ["missing"] = "1" }, scenario, out _, out _, out _));
        Assert.False(PuppetTuningBuilder.Defaults().ContainsKey("PuppetSettings.arenaHalfSize"));
        Assert.False(PuppetTuningBuilder.Defaults().ContainsKey("PuppetSettings.ticksPerSecond"));
    }
    [Fact]
    public void CartesianProductAndLimit()
    {
        Assert.True(PuppetSweep.TryExpand(new[] { "a=1,2", "b=3,4,5" }, 64, out var rows, out _));
        Assert.Equal(6, rows.Count); Assert.Equal(6, rows.Select(r => r["a"] + r["b"]).Distinct().Count());
        Assert.False(PuppetSweep.TryExpand(new[] { "a=" + string.Join(",", Enumerable.Range(0,65)) },64,out _,out _));
        Assert.True(PuppetSweep.TryExpand(Array.Empty<string>(),64,out rows,out _)); Assert.Single(rows);
    }
    [Fact]
    public void BuilderRoundsIntegersAndRecursesIntoSurround()
    {
        Assert.True(PuppetTuningBuilder.TryBuild(new Dictionary<string,string> {
            ["BasicActionSettings.harassIntervalTicks"]="44.6", ["SurroundSettings.attackerWeight"]="0.6"
        },new(),out var settings,out var differences,out _));
        Assert.Equal(45,settings!.HarassIntervalTicks); Assert.Equal(.6,settings.RoleWeights[TargetRole.Attacker][ActionIds.Surround]);
        Assert.Contains("BasicActionSettings.harassIntervalTicks=45",differences);
        foreach(string value in new[]{"NaN","Infinity","1e30","0"})
            Assert.False(PuppetTuningBuilder.TryBuild(new Dictionary<string,string>{["BasicActionSettings.harassIntervalTicks"]=value},new(),out _,out _,out _));
    }
    private static PuppetFrame Frame(int tick, double x = 0, string action = ActionIds.Approach, int target = 1, double playerZ = 1, double z = 0)
        => new(tick, new Vec2(0,playerZ), new[] { new AgentState(new AgentId(10), Side.Puppets,false,TargetRole.Guard,new Vec2(x,z),new Vec2(0,1),.3,action,new AgentId(target)) });
    [Theory]
    [InlineData(5,ActionIds.Approach,0)] [InlineData(6,ActionIds.Approach,1)]
    [InlineData(45,ActionIds.Approach,1)] [InlineData(46,ActionIds.Approach,0)]
    [InlineData(6,ActionIds.Hold,0)] [InlineData(6,ActionIds.Attack,0)] [InlineData(6,ActionIds.Intercept,0)]
    public void StutterBoundariesAndNormalStops(int stopped, string action, int expected)
    {
        var r = new PuppetTrajectory(); r.Frames.Add(Frame(0)); r.Frames.Add(Frame(1,1));
        for(int t=2;t<stopped+2;t++) r.Frames.Add(Frame(t,1,action));
        r.Frames.Add(Frame(stopped+2,2)); Assert.Equal(expected,SweepMetrics.Stutter(r));
    }
    [Theory]
    [InlineData(HarassPhase.Waiting,1)] [InlineData(HarassPhase.Withdraw,1)]
    [InlineData(HarassPhase.Strike,0)] [InlineData(HarassPhase.Recover,0)]
    public void HarassStutterRequiresPressurePhaseThroughout(HarassPhase phase,int expected)
    {
        var r=new PuppetTrajectory(); r.Frames.Add(Frame(0)); r.Frames.Add(Frame(1,1));
        for(int t=2;t<=7;t++) r.Frames.Add(Frame(t,1,ActionIds.Harass) with { HarassPhases=new Dictionary<int,HarassPhase?>{[10]=phase} });
        r.Frames.Add(Frame(8,2)); Assert.Equal(expected,SweepMetrics.Stutter(r));
        r.Frames[4]=Frame(4,1,ActionIds.Hold); Assert.Equal(0,SweepMetrics.Stutter(r));
    }
    [Fact]
    public void ThreeEventLatenciesUseTickDifferencesAndNoSwitchIsZero()
    {
        var r = new PuppetTrajectory();
        for(int t=0;t<=20;t++) r.Frames.Add(Frame(t,action:t<8?ActionIds.Approach:ActionIds.Hold,target:t<7?1:2));
        Assert.Equal(2, SweepMetrics.TargetLatency(r,5));
        Assert.Equal(3, SweepMetrics.StandstillLatency(r,5,5));
        Assert.Equal(0, SweepMetrics.StandstillLatency(r,9,5));
        r.Frames.Clear();
        for(int t=0;t<=20;t++) r.Frames.Add(Frame(t,action:ActionIds.Intercept,playerZ:5-t,z:t<9?2:0));
        Assert.Equal(4,SweepMetrics.PassbyLatency(r,new Vec2(-1,0),new Vec2(1,0)));
    }
    [Fact]
    public void MissingLatencyIsNotReportedAsInstantReaction()
    {
        var r=new PuppetTrajectory(); for(int t=0;t<5;t++)r.Frames.Add(Frame(t));
        Assert.Null(SweepMetrics.TargetLatency(r,2)); Assert.Null(SweepMetrics.StandstillLatency(r,2,5));
        Assert.Null(SweepMetrics.PassbyLatency(r,new Vec2(-1,0),new Vec2(1,0)));
    }
    [Fact]
    public void BaselineAggregatesFifteenRunsAndThreePassbyEvents()
    {
        var s=new PuppetScenarioSettings(); int failures=0,stutter=0,total=0,runs=0;
        var swaps=new List<double>();var stops=new List<double>();var passes=new List<double>();
        for(ulong seed=1;seed<=3;seed++)
        {
            foreach(string name in new[]{"approach","healer","standstill","duo","duo-swap"})
            {
                bool duo=name is "duo" or "duo-swap";
                var r=PuppetSimulation.Run(name,seed,null,null,s,"none",duo,loadout:duo?"mixed":"presets");
                failures+=SweepMetrics.Forbidden(r,s);stutter+=SweepMetrics.Stutter(r);
                var d=SweepMetrics.ActionDurations(r);total+=d.Total;runs+=d.Runs;
                if(name=="duo-swap") {var v=SweepMetrics.TargetLatency(r,150);if(v.HasValue)swaps.Add(v.Value);}
                if(name=="standstill")stops.Add(SweepMetrics.StandstillLatency(r,r.Frames.First(f=>f.Player.Z<=s.StandstillStop).Tick,30)!.Value);
            }
            passes.Add(SweepMetrics.PassbyLatency(PuppetSimulation.Run("passby",seed),s.FirstGuardStart,s.SecondGuardStart)!.Value);
        }
        using var w=new StringWriter();Assert.Equal(0,PuppetSweep.Run(Array.Empty<string>(),w,new StringWriter()));
        string expected=FormattableString.Invariant($"baseline | {failures} | {stutter} | {swaps.Average():F2} | {stops.Average():F2} | {passes.Average():F2} | {(swaps.Average()+stops.Average()+passes.Average())/3:F2} | {(double)total/runs:F2} | ");
        Assert.Contains(expected,w.ToString());Assert.Equal(3,w.ToString().Split('\n',StringSplitOptions.RemoveEmptyEntries).Length);
    }
    [Fact]
    public void TableSortsPassingCandidatesThenStutterAndLatencyWithRejectedLast()
    {
        using var w=new StringWriter();
        PuppetSweep.Run(new[]{"PuppetSettings.reevaluationIntervalTicks=10,15,20,30,0"},w,new StringWriter());
        var rows=w.ToString().Split('\n',StringSplitOptions.RemoveEmptyEntries).Skip(2).Select(l=>l.Split('|').Select(c=>c.Trim()).ToArray()).ToArray();
        Assert.Equal("0",rows[0][1]);Assert.Equal("rejected",rows.Last()[1]);
        var valid=rows.Take(rows.Length-1).Select(r=>(Failed:int.Parse(r[1])!=0,Stutter:int.Parse(r[2]),Latency:double.Parse(r[6],System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        Assert.Equal(valid.OrderBy(r=>r.Failed).ThenBy(r=>r.Stutter).ThenBy(r=>r.Latency),valid);
        using var e=new StringWriter();using var noOutput=new StringWriter();
        Assert.Equal(2,PuppetSweep.Run(new[]{"a="+string.Join(",",Enumerable.Range(0,65))},noOutput,e));Assert.Equal("",noOutput.ToString());
    }
    [Fact]
    public void SweepTableIsDeterministicAndRejectsOnlyInvalidCombinations()
    {
        string Run() { using var w=new StringWriter(); using var e=new StringWriter(); Assert.Equal(0,Simulation.Run(new[] {"--sweep","BasicActionSettings.harassIntervalTicks=45,0,abc"},w,e)); Assert.Equal("",e.ToString()); return w.ToString(); }
        var first=Run(); Assert.Equal(first,Run()); Assert.Equal(2, first.Split('\n').Count(l=>l.Contains("rejected"))); Assert.Contains("baseline |",first);
    }
    [Theory]
    [InlineData("approach")] [InlineData("healer")] [InlineData("standstill")] [InlineData("duo")] [InlineData("duo-swap")] [InlineData("passby")]
    public void DefaultBuilderPreservesExistingTrajectoriesAndCommandOutput(string name)
    {
        Assert.True(PuppetTuningBuilder.TryBuild(new Dictionary<string,string>(),new(),out var settings,out _,out _));
        var expected=PuppetSimulation.Run(name); var actual=PuppetSimulation.Run(name,settings:settings);
        Assert.Equal(InterceptRegressionTests.Hash(expected),InterceptRegressionTests.Hash(actual));
        using var direct=new StringWriter(); PuppetSimulation.Print(expected,direct);
        using var command=new StringWriter(); using var error=new StringWriter();
        Assert.Equal(0,Simulation.Run(new[]{"--puppets",name},command,error)); Assert.Equal(direct.ToString(),command.ToString());
    }
}
