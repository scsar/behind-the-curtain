using System;
using System.Linq;
using BehindTheCurtain.Core.Sim;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class SurroundTests
{
    private static AgentState Agent(int id, Vec2 p, Side side = Side.Puppets,
        TargetRole role = TargetRole.Attacker, string? action = null, int? target = null)
        => new(new AgentId(id), side, side == Side.Players, role, p, new Vec2(0, 1), .3,
            action, target.HasValue ? new AgentId(target.Value) : null);
    private static PuppetBrain Brain(World w, PuppetSettings s, ulong seed = 1)
        => PuppetBrain.Create(w, s, new DeterministicRandom(seed),
            new DirectorProfile(enableWaver: false, tuneBudgetPerSecond: 0)).Value;

    [Theory]
    [InlineData(1, 5, 0)] [InlineData(2, 5, 1)]
    [InlineData(2, 2, 0)] [InlineData(2, 6, 1)] [InlineData(2, 6.01, 0)]
    public void SurroundSuitabilityRequiresTwoOtherPressurersAndDistanceBand(int others, double distance, double expected)
    {
        var w = new World(); w.Add(Agent(1, new Vec2(0, 0), Side.Players));
        w.Add(Agent(2, new Vec2(20, 0), Side.Players));
        for (int i = 0; i < others; i++) w.Add(Agent(10 + i, new Vec2(i, 1), action: ActionIds.Approach, target: 1));
        w.Add(Agent(20, new Vec2(0, distance)));
        var b = Brain(w, new PuppetSettings(moveSpeed: 0, weightJitter: 0)); b.Tick(0);
        Assert.Equal(expected, b.GetDecision(new AgentId(20)).Value.Selection.Scores.Single(s => s.ActionId == ActionIds.Surround).Product);
    }

    private static (World, PuppetBrain) Ring(ulong seed = 1)
    {
        var w = new World(); w.Add(Agent(1, new Vec2(0, 0), Side.Players));
        w.Add(Agent(2, new Vec2(20, 0), Side.Players));
        // Uneven initial directions must spread into the reserved ring.
        for (int i = 0; i < 3; i++)
        {
            double angle = (new[] { 0, 90, 220 }[i]) * Math.PI / 180;
            w.Add(Agent(10 + i, new Vec2(5 * Math.Cos(angle), 5 * Math.Sin(angle)), action: ActionIds.Approach, target: 1));
        }
        return (w, Brain(w, new PuppetSettings(new BasicActionSettings(new SurroundSettings(1, 1), flankWeight: 0, harassWeight: 0),
            approachWeight: 0, spreadWeight: 0, weightJitter: 0, moveSpeed: 3, reevaluationIntervalTicks: 1), seed));
    }

    [Fact]
    public void SurroundReservesDistinctEvenSlotsAfterTwoSecondsAndNeverExpandsItsArc()
    {
        var (w, b) = Ring(); int settled = 0; bool attacked = false;
        for (int t = 0; t < 1200; t++)
        {
            var before = w.GetAgents().Where(a => a.Side == Side.Puppets).ToArray();
            Assert.Equal(AiFailure.None, b.Tick(t));
            var after = w.GetAgents().Where(a => a.Side == Side.Puppets).ToArray();
            foreach (var a in after.Where(a => a.CurrentAction == ActionIds.Surround))
                Assert.True(PuppetSimulation.Distance(a.Position, new Vec2(0, 0)) <=
                    PuppetSimulation.Distance(before.Single(x => x.Id.Equals(a.Id)).Position, new Vec2(0, 0)) + .3);
            if (t >= 60 && after.All(a => a.CurrentAction == ActionIds.Surround))
            {
                settled++;
                var slots = after.Select(a => b.GetDecision(a.Id).Value.SurroundSlot!.Value).ToArray();
                Assert.Equal(3, slots.Distinct().Count());
                foreach (var a in after)
                {
                    int slot = b.GetDecision(a.Id).Value.SurroundSlot!.Value;
                    var r = w.GetBlackboard(Side.Puppets).Value.GetReservation("surround:1:" + slot, t);
                    Assert.True(r.Success); Assert.Equal(a.Id, r.Value.Owner);
                }
                var angles = after.Select(a => (Math.Atan2(a.Position.Z, a.Position.X) * 180 / Math.PI + 360) % 360).OrderBy(x => x).ToArray();
                for (int i = 0; i < 3; i++) Assert.InRange((angles[(i + 1) % 3] - angles[i] + 360) % 360, 95, 145);
            }
            attacked |= after.Any(a => a.CurrentAction == ActionIds.Attack);
        }
        Assert.True(settled > 0); Assert.True(attacked);
    }

    [Fact]
    public void SurroundRingAndReservationsAreDeterministic()
    {
        var (a, ab) = Ring(82); var (b, bb) = Ring(82);
        for (int t = 0; t < 1200; t++)
        {
            ab.Tick(t); bb.Tick(t);
            foreach (int id in new[] { 10, 11, 12 })
            {
                Assert.Equal(a.Get(new AgentId(id)).Value.Position, b.Get(new AgentId(id)).Value.Position);
                Assert.Equal(ab.GetDecision(new AgentId(id)).Value.SurroundSlot, bb.GetDecision(new AgentId(id)).Value.SurroundSlot);
                Assert.Equal(ab.GetDecision(new AgentId(id)).Value.Selection.ActionId, bb.GetDecision(new AgentId(id)).Value.Selection.ActionId);
            }
        }
    }

    [Fact]
    public void HarassIgnoresKeepDistanceAndInterceptTargets()
    {
        var w = new World(); w.Add(Agent(1, new Vec2(0, 0), Side.Players));
        w.Add(Agent(2, new Vec2(10, 0), Side.Players));
        w.Add(Agent(10, new Vec2(1, 0), action: ActionIds.KeepDistance, target: 1));
        w.Add(Agent(11, new Vec2(2, 0), action: ActionIds.Intercept, target: 1));
        w.Add(Agent(12, new Vec2(9, 0), action: ActionIds.Approach, target: 2));
        w.Add(Agent(5, new Vec2(6, 0)));
        var b = Brain(w, new PuppetSettings(new BasicActionSettings(new SurroundSettings(0, 0), flankWeight: 0, harassWeight: 1),
            attackWeight: 0, approachWeight: 0, spreadWeight: 0, weightJitter: 0, pressCap: 1));
        b.Tick(0); Assert.Equal(1, b.GetDecision(new AgentId(5)).Value.Target!.Value.Value);
    }

    [Fact]
    public void HarassRetainsTargetAcrossNewWaitingBelowPressCap()
    {
        var w = new World(); w.Add(Agent(1, new Vec2(0, 0), Side.Players));
        w.Add(Agent(2, new Vec2(10, 0), Side.Players));
        w.Add(Agent(10, new Vec2(10, 3), action: ActionIds.Approach, target: 2));
        w.Add(Agent(20, new Vec2(0, 3)));
        var b = Brain(w, new PuppetSettings(new BasicActionSettings(new SurroundSettings(0, 0), flankWeight: 0, harassWeight: 1,
            harassIntervalTicks: 3, harassRecoverTicks: 3), attackWeight: 0, approachWeight: 0,
            spreadWeight: 0, weightJitter: 0, reevaluationIntervalTicks: 1));
        bool strike = false, recover = false, withdraw = false, restarted = false;
        for (int t = 0; t < 100; t++)
        {
            if (t > 0) w.UpdateAction(new AgentId(10), ActionIds.Approach, new AgentId(1));
            b.Tick(t); var d = b.GetDecision(new AgentId(20)).Value;
            if (t == 0) Assert.Equal(1, d.Target!.Value.Value);
            if (d.HarassPhase == HarassPhase.Strike) strike = true;
            if (d.HarassPhase == HarassPhase.Recover) recover = true;
            if (d.HarassPhase == HarassPhase.Withdraw) withdraw = true;
            if (withdraw && d.HarassPhase == HarassPhase.Waiting)
            { Assert.Equal(1, d.Target!.Value.Value); restarted = true; break; }
            Assert.Equal(1, d.Target!.Value.Value);
        }
        Assert.True(strike && recover && withdraw && restarted);
    }

    [Theory]
    [InlineData(ActionIds.Intercept, ActionIds.Surround, 0, 1)]
    [InlineData(ActionIds.Approach, ActionIds.Approach, 1, 0)]
    [InlineData(ActionIds.Intercept, ActionIds.Approach, 0, 1)]
    public void ReservationTransitionsCountAsCoordination(string a, string b, int swarm, int coordination)
    {
        var r = new PuppetTrajectory(); PuppetSimulation.RecordActionChanges(r, new[] { a, b });
        Assert.Equal(1, r.ChangedTicks); Assert.Equal(swarm, r.SimultaneousTicks);
        Assert.Equal(coordination, r.CoordinatedTicks);
        Assert.Equal(swarm, r.SimultaneousRatio); Assert.Equal(coordination, r.CoordinatedRatio);
    }

    [Fact]
    public void MixedTransitionsCanContainBothCoordinationAndSwarmButSingleChangesCannot()
    {
        var r = new PuppetTrajectory();
        PuppetSimulation.RecordActionChanges(r, new[] { ActionIds.Surround, ActionIds.Approach, ActionIds.Approach });
        Assert.Equal(1, r.SimultaneousTicks); Assert.Equal(1, r.CoordinatedTicks);
        PuppetSimulation.RecordActionChanges(r, new[] { ActionIds.Intercept });
        PuppetSimulation.RecordActionChanges(r, Array.Empty<string>());
        Assert.Equal(2, r.ChangedTicks); Assert.Equal(.5, r.SimultaneousRatio); Assert.Equal(.5, r.CoordinatedRatio);
    }

    [Fact]
    public void InterceptQualityRetainsHealerAndOrbitBaseline()
    {
        var healer = PuppetSimulation.Run("healer"); var orbit = PuppetSimulation.Run("orbit");
        Assert.True(healer.InterceptQualityRatio >= .8);
        Assert.Equal(1, orbit.InterceptQualityRatio);
        Assert.True(orbit.InterceptTravel >= 9.2 - 1e-8);
    }

    [Fact]
    public void SurroundIsBasicAndHasConfiguredRoleAndWeaponWeights()
    {
        Assert.True(RoleContract.IsBasicAction(ActionIds.Surround));
        Assert.Contains(ActionIds.Surround, RoleContract.ForRole(TargetRole.Attacker).Actions);
        Assert.Contains(ActionIds.Surround, RoleContract.ForRole(TargetRole.Guard).Actions);
        var s = new PuppetSettings(); Assert.Equal(.35, s.GetRoleWeight(TargetRole.Attacker, ActionIds.Surround));
        Assert.Equal(.2, s.GetRoleWeight(TargetRole.Guard, ActionIds.Surround));
        Assert.Equal(0, s.GetRoleWeight(TargetRole.Healer, ActionIds.Surround));
        foreach (string weapon in new[] { "weapon.melee", "weapon.spear", "weapon.bow" })
        {
            var w = new World(); w.Add(Agent(1, new Vec2(0, 0), Side.Players));
            w.AddWithLoadout(Agent(20, new Vec2(0, 5)), Loadout.Create(new[] { weapon }).Value);
            var b = Brain(w, new PuppetSettings(weightJitter: 0)); b.Tick(0);
            Assert.Equal(weapon == "weapon.melee" ? .35 : .35 * s.FlankNonMeleeScale,
                b.GetDecision(new AgentId(20)).Value.Selection.Scores.Single(x => x.ActionId == ActionIds.Surround).BaseWeight, 8);
        }
        Assert.Equal(AiFailure.InvalidSettings, PuppetBrain.Create(new World(),
            new PuppetSettings(new BasicActionSettings(new SurroundSettings(double.NaN, .2))), new DeterministicRandom(1)).Failure);
    }
}
