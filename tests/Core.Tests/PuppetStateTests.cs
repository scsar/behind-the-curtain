using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class PuppetStateTests
{
    private static readonly AgentId Id = new(10);
    private static AgentState Agent(int id = 10, TargetRole role = TargetRole.Attacker, double x = 0,
        Side side = Side.Puppets) => new(new AgentId(id), side, side == Side.Players, role,
            new Vec2(x, 0), new Vec2(1, 0), 0.3);
    private static World Make(PuppetKind kind = PuppetKind.Puppet, PuppetStateSettings? settings = null,
        double health = 100, double posture = 100, double tension = 1)
    {
        var world = new World(settings ?? new PuppetStateSettings());
        Assert.True(world.Add(Agent(), new PuppetInitialState(kind, new Dictionary<PuppetValueId, double> {
            [PuppetValueId.Health] = health, [PuppetValueId.Posture] = posture,
            [PuppetValueId.Tension] = tension })).Success);
        return world;
    }
    private static PuppetBrain Brain(World world, int ticksPerSecond = 30, ulong seed = 1514)
        => PuppetBrain.Create(world, new PuppetSettings(ticksPerSecond: ticksPerSecond, weightJitter: 0),
            new DeterministicRandom(seed), new DirectorProfile(tuneBudgetPerSecond: 0, enableWaver: false)).Value;
    private static PuppetState State(World world) => world.GetPuppetState(Id).Value;

    [Fact]
    public void DefaultsAndOpenValuesAreImmutableAndIndependentOfPlayerHumanFlag()
    {
        var world = new World();
        Assert.True(world.Add(Agent()).Success);
        Assert.Equal(PuppetKind.Puppet, State(world).Kind);
        Assert.Equal(PuppetStatus.Active, State(world).Status);
        Assert.Equal(100, State(world).Health);
        Assert.Equal(100, State(world).Posture);
        Assert.Equal(1, State(world).Tension);
        var values = new Dictionary<PuppetValueId, double> { [(PuppetValueId)100] = 7 };
        var initial = new PuppetInitialState(PuppetKind.Humanized, values);
        values[(PuppetValueId)100] = 9;
        Assert.True(world.Add(Agent(11), initial).Success);
        Assert.Equal(7, world.GetPuppetState(new AgentId(11)).Value[(PuppetValueId)100]);
        var snapshot = State(world);
        world.ApplyDamage(Id, 2, 3, 0);
        Assert.Equal(100, snapshot.Health);
        Assert.Equal(100, snapshot.Posture);
        Assert.Equal(new[] { 10, 11 }, world.GetPuppetStates().Select(s => s.Id.Value));
    }

    [Theory]
    [InlineData(PuppetKind.Puppet, 100, PuppetStatus.Broken)]
    [InlineData(PuppetKind.Humanized, 100, PuppetStatus.Downed)]
    [InlineData(PuppetKind.Puppet, 99.9999, PuppetStatus.Active)]
    [InlineData(PuppetKind.Humanized, 99.9999, PuppetStatus.Active)]
    public void HealthBoundary(PuppetKind kind, double damage, PuppetStatus expected)
    {
        var world = Make(kind);
        Assert.True(world.ApplyDamage(Id, damage, 0, 0).Success);
        Assert.Equal(expected, State(world).Status);
        Assert.Equal(100 - damage, State(world).Health, 8);
    }

    [Theory]
    [InlineData(PuppetKind.Puppet, 1, PuppetStatus.Released)]
    [InlineData(PuppetKind.Humanized, 1, PuppetStatus.Groggy)]
    [InlineData(PuppetKind.Puppet, 0.9999, PuppetStatus.Active)]
    [InlineData(PuppetKind.Humanized, 0.9999, PuppetStatus.Active)]
    public void TensionBoundary(PuppetKind kind, double amount, PuppetStatus expected)
    {
        var world = Make(kind);
        Assert.True(world.ReduceTension(Id, amount, 0).Success);
        Assert.Equal(expected, State(world).Status);
        Assert.Equal(1 - amount, State(world).Tension, 8);
    }

    [Fact]
    public void GroggyStopsCachedDecisionsAndMovementUntilExactDeadlineAndKeepsCause()
    {
        var world = Make(PuppetKind.Humanized);
        world.Add(Agent(1, x: 8, side: Side.Players));
        var brain = Brain(world);
        Assert.Equal(AiFailure.None, brain.Tick(0));
        var position = world.Get(Id).Value.Position;
        Assert.True(brain.GetDecision(Id).Success);
        world.ReduceTension(Id, 1, 1);
        var cause = world.StateEvents.Last().Cause;
        Assert.False(brain.GetDecision(Id).Success);
        Assert.Null(world.Get(Id).Value.CurrentAction);
        for (int tick = 1; tick <= 60; tick++)
        {
            Assert.Equal(AiFailure.None, brain.Tick(tick));
            Assert.Equal(PuppetStatus.Groggy, State(world).Status);
            Assert.False(brain.GetDecision(Id).Success);
            Assert.Equal(position, world.Get(Id).Value.Position);
        }
        Assert.Equal(AiFailure.None, brain.Tick(61));
        Assert.Equal(PuppetStatus.Active, State(world).Status);
        Assert.Equal(0.5, State(world).Tension);
        Assert.True(brain.GetDecision(Id).Success);
        Assert.NotEqual(position, world.Get(Id).Value.Position);
        Assert.Same(cause, world.StateEvents.Last().Cause);
        Assert.Equal(61, world.StateEvents.Last().Tick);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void BadAmountsRejectAtomically(double amount)
    {
        var world = Make();
        var before = State(world);
        int count = world.StateEvents.Count;
        Assert.False(world.ApplyDamage(Id, amount, 0, 0).Success);
        Assert.False(world.ApplyDamage(Id, 0, amount, 0).Success);
        Assert.False(world.ReduceTension(Id, amount, 0).Success);
        Assert.Same(before, State(world));
        Assert.Equal(count, world.StateEvents.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    [InlineData(1)]
    public void MissingOrPlayerIdRejects(int id)
    {
        var world = Make();
        world.Add(Agent(1, side: Side.Players));
        Assert.False(world.ApplyDamage(new AgentId(id), 1, 1, 0).Success);
        Assert.False(world.ReduceTension(new AgentId(id), 1, 0).Success);
    }

    [Theory]
    [InlineData(PuppetKind.Puppet, true, PuppetStatus.Broken)]
    [InlineData(PuppetKind.Puppet, false, PuppetStatus.Released)]
    [InlineData(PuppetKind.Humanized, true, PuppetStatus.Downed)]
    public void TerminalStatesRejectEveryInputAndNeverReconnect(PuppetKind kind, bool damage, PuppetStatus expected)
    {
        var world = Make(kind);
        var brain = Brain(world);
        if (damage) world.ApplyDamage(Id, 100, 0, 0);
        else world.ReduceTension(Id, 1, 0);
        int count = world.StateEvents.Count;
        for (int tick = 0; tick <= 120; tick++)
        {
            Assert.False(world.ApplyDamage(Id, tick % 2, tick % 3, tick).Success);
            Assert.False(world.ReduceTension(Id, tick % 2, tick).Success);
            Assert.Equal(AiFailure.None, brain.Tick(tick));
            Assert.Equal(expected, State(world).Status);
            Assert.False(brain.GetDecision(Id).Success);
        }
        Assert.Equal(count + (damage ? 1 : 0), world.StateEvents.Count);
        Assert.Empty(world.GetParticipatingAgents());
        Assert.Single(world.GetAgents());
    }

    [Fact]
    public void GroggyAcceptsDamageAndDownedCannotBeRevivedByPendingReconnect()
    {
        var world = Make(PuppetKind.Humanized);
        var brain = Brain(world);
        world.ReduceTension(Id, 1, 0);
        Assert.False(world.ReduceTension(Id, 0, 0).Success);
        Assert.True(world.ApplyDamage(Id, 1, 1, 0).Success);
        Assert.Equal(PuppetStatus.Groggy, State(world).Status);
        Assert.True(world.ApplyDamage(Id, 99, 0, 0).Success);
        for (int tick = 0; tick < 100; tick++)
        {
            Assert.Equal(AiFailure.None, brain.Tick(tick));
            Assert.False(world.ReduceTension(Id, 0, tick).Success);
            Assert.False(world.ApplyDamage(Id, 0, 0, tick).Success);
        }
        Assert.Equal(PuppetStatus.Downed, State(world).Status);
    }

    [Theory]
    [InlineData(29.9, 0.05)]
    [InlineData(30, 0.1)]
    public void PostureSlowThresholdIsStrict(double posture, double movement)
    {
        var world = Make(posture: posture);
        world.Add(Agent(1, x: 8, side: Side.Players));
        var brain = Brain(world);
        Assert.Equal(AiFailure.None, brain.Tick(0));
        Assert.Equal(ActionIds.Approach, brain.GetDecision(Id).Value.Selection.ActionId);
        Assert.Equal(movement, world.Get(Id).Value.Position.X, 10);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(30)]
    [InlineData(60)]
    public void RecoveryUsesTickRateAndCapsAtMaximum(int rate)
    {
        var world = Make(posture: 85);
        var brain = Brain(world, rate);
        for (int tick = 0; tick <= rate; tick++) Assert.Equal(AiFailure.None, brain.Tick(tick));
        Assert.Equal(95, State(world).Posture, 8);
        for (int tick = rate + 1; tick <= rate * 3; tick++) brain.Tick(tick);
        Assert.Equal(100, State(world).Posture);
        Assert.All(world.StateEvents.Where(e => e.Kind == PuppetStateEventKind.ValueChanged),
            e => Assert.Equal(PuppetInputKind.RecoverPosture, e.Cause.Kind));
    }

    [Fact]
    public void RepeatedDamageDoesNotDiscardRecoveryAndGroggyRecoversPosture()
    {
        var world = Make(PuppetKind.Humanized, posture: 50);
        var brain = Brain(world);
        world.ReduceTension(Id, 1, 0);
        brain.Tick(0);
        for (int tick = 1; tick <= 30; tick++)
        {
            Assert.True(world.ApplyDamage(Id, 0, 0.1, tick).Success);
            Assert.Equal(AiFailure.None, brain.Tick(tick));
        }
        Assert.Equal(57, State(world).Posture, 8);
        Assert.Equal(PuppetStatus.Groggy, State(world).Status);
    }

    [Fact]
    public void InputEventsPrecedeChangesAndShareExactCause()
    {
        var world = Make(PuppetKind.Humanized);
        int start = world.StateEvents.Count;
        world.ApplyDamage(Id, 100, 12, 7);
        var events = world.StateEvents.Skip(start).ToArray();
        Assert.Equal(new[] { PuppetStateEventKind.Input, PuppetStateEventKind.ValueChanged,
            PuppetStateEventKind.ValueChanged, PuppetStateEventKind.StatusChanged }, events.Select(e => e.Kind));
        Assert.All(events, e => {
            Assert.Equal(7, e.Tick); Assert.Equal(Id, e.AgentId); Assert.Same(events[0].Cause, e.Cause);
        });
        Assert.Equal(PuppetInputKind.ApplyDamage, events[0].Cause.Kind);
        Assert.Equal(100, events[0].Cause.Health);
        Assert.Equal(12, events[0].Cause.Posture);
        Assert.Equal(100, events[1].Before.Health);
        Assert.Equal(0, events[1].After.Health);
        Assert.Equal(PuppetStatus.Active, events[3].Before.Status);
        Assert.Equal(PuppetStatus.Downed, events[3].After.Status);
    }

    [Theory]
    [InlineData(PuppetKind.Puppet, true)]
    [InlineData(PuppetKind.Puppet, false)]
    [InlineData(PuppetKind.Humanized, true)]
    public void RemovedHealerClearsCachedInterceptAndReservations(PuppetKind kind, bool damage)
    {
        var world = new World();
        world.Add(Agent(1, x: 8, side: Side.Players));
        world.Add(Agent(10, TargetRole.Guard, -4));
        world.Add(Agent(11, TargetRole.Healer), new PuppetInitialState(kind));
        var board = world.GetBlackboard(Side.Puppets).Value;
        board.SetFact("tactic", TacticId.GuardHealer);
        board.SetFact(TargetFactIds.HealerThreat, new AgentId(1));
        board.SetIntention(ActionIds.Intercept, 100);
        var brain = Brain(world);
        brain.Tick(0);
        Assert.Equal(ActionIds.Intercept, brain.GetDecision(Id).Value.Selection.ActionId);
        Assert.True(board.GetReservation("intercept:11", 0).Success);
        var healer = new AgentId(11);
        if (damage) world.ApplyDamage(healer, 100, 0, 1);
        else world.ReduceTension(healer, 1, 1);
        Assert.False(board.GetReservation("intercept:11", 1).Success);
        Assert.False(board.Reserve("intercept:11", Id, 100, 1).Success);
        Assert.Equal(AiFailure.None, brain.Tick(1));
        Assert.False(brain.GetDecision(healer).Success);
        Assert.NotEqual(ActionIds.Intercept, brain.GetDecision(Id).Value.Selection.ActionId);
        Assert.Equal(0, brain.GetDecision(Id).Value.Selection.Scores.Single(s => s.ActionId == ActionIds.Intercept).Score);
        Assert.False(board.GetReservation("intercept:11", 1).Success);
    }

    [Theory]
    [InlineData(PuppetKind.Puppet, true)]
    [InlineData(PuppetKind.Puppet, false)]
    [InlineData(PuppetKind.Humanized, true)]
    public void RemovedNeighborHasNoSpreadInfluenceOrOwnedReservation(PuppetKind kind, bool damage)
    {
        var world = Make();
        world.Add(Agent(11, x: 0.1), new PuppetInitialState(kind));
        var board = world.GetBlackboard(Side.Puppets).Value;
        board.SetIntention(ActionIds.Spread, 100);
        board.Reserve("custom", new AgentId(11), 100, 0);
        if (damage) world.ApplyDamage(new AgentId(11), 100, 0, 0);
        else world.ReduceTension(new AgentId(11), 1, 0);
        var brain = Brain(world);
        brain.Tick(0);
        Assert.Equal(0, brain.GetDecision(Id).Value.Selection.Scores.Single(s => s.ActionId == ActionIds.Spread).Score);
        Assert.Equal(new Vec2(0, 0), world.Get(Id).Value.Position);
        Assert.False(board.GetReservation("custom", 0).Success);
        Assert.False(board.Reserve("custom", new AgentId(11), 100, 0).Success);
    }

    [Fact]
    public void GroggyOwnerReleasesReservationSoAnotherGuardCanProtectHealer()
    {
        var world = Make(PuppetKind.Humanized);
        world.Add(Agent(11, TargetRole.Healer));
        world.Add(Agent(12, TargetRole.Guard));
        var board = world.GetBlackboard(Side.Puppets).Value;
        Assert.True(board.Reserve("intercept:11", Id, 100, 0).Success);
        world.ReduceTension(Id, 1, 0);
        Assert.False(board.GetReservation("intercept:11", 0).Success);
        Assert.False(board.Reserve("intercept:11", Id, 100, 0).Success);
        Assert.True(board.Reserve("intercept:11", new AgentId(12), 100, 0).Success);
        Assert.True(world.IsParticipating(Id));
        Assert.False(world.CanAct(Id));
    }

    [Fact]
    public void PositiveTensionReductionNowSelectsStationaryWaver()
    {
        var loose = Make();
        loose.Add(Agent(1, x: 8, side: Side.Players));
        var brain = PuppetBrain.Create(loose, new PuppetSettings(), new DeterministicRandom(1),
            new DirectorProfile(tuneBudgetPerSecond: 0)).Value;
        loose.ReduceTension(Id, 0.9999, 0);
        var position = loose.Get(Id).Value.Position;
        for (int tick = 0; tick < 90; tick++)
        {
            brain.Tick(tick);
            Assert.Equal(ActionIds.Waver, brain.GetDecision(Id).Value.Selection.ActionId);
            Assert.Equal(position, loose.Get(Id).Value.Position);
        }
    }

    [Theory]
    [InlineData(PuppetKind.Puppet, true)]
    [InlineData(PuppetKind.Puppet, false)]
    [InlineData(PuppetKind.Humanized, true)]
    public void RemovedNeighborDoesNotChangeLiveSpreadVector(PuppetKind kind, bool damage)
    {
        World Setup(bool addRemoved)
        {
            var world = Make();
            world.Add(Agent(11, x: 1));
            world.GetBlackboard(Side.Puppets).Value.SetIntention(ActionIds.Spread, 100);
            if (addRemoved)
            {
                world.Add(Agent(12, x: -0.1), new PuppetInitialState(kind));
                if (damage) world.ApplyDamage(new AgentId(12), 100, 0, 0);
                else world.ReduceTension(new AgentId(12), 1, 0);
            }
            return world;
        }
        var a = Setup(false);
        var b = Setup(true);
        var brainA = Brain(a);
        var brainB = Brain(b);
        brainA.Tick(0); brainB.Tick(0);
        Assert.Equal(ActionIds.Spread, brainB.GetDecision(Id).Value.Selection.ActionId);
        Assert.Equal(brainA.GetDecision(Id).Value.Goal, brainB.GetDecision(Id).Value.Goal);
        Assert.Equal(a.Get(Id).Value.Position, b.Get(Id).Value.Position);
    }

    [Fact]
    public void RemovedGuardCannotPullHealerKeepDistanceDestination()
    {
        var world = new World();
        world.Add(Agent(1, x: 0, side: Side.Players));
        world.Add(Agent(10, TargetRole.Healer, 6));
        world.Add(Agent(11, TargetRole.Guard, -6));
        var brain = Brain(world);
        world.ApplyDamage(new AgentId(11), 100, 0, 0);
        world.GetBlackboard(Side.Puppets).Value.SetIntention(ActionIds.KeepDistance, 100);
        brain.Tick(0);
        Assert.Equal(ActionIds.KeepDistance, brain.GetDecision(Id).Value.Selection.ActionId);
        Assert.Equal(new Vec2(6, 0), brain.GetDecision(Id).Value.Goal);
        Assert.Equal(new Vec2(6, 0), world.Get(Id).Value.Position);
    }

    [Fact]
    public void DirectSelectorAlsoRejectsInactiveAgentWithoutEvaluatingCandidates()
    {
        var world = Make(PuppetKind.Humanized);
        var selector = UtilitySelector.Create(world, Id, Array.Empty<UtilityAction>(),
            new UtilitySettings(), new DeterministicRandom(1)).Value;
        world.ReduceTension(Id, 1, 0);
        Assert.Equal(AiFailure.InvalidState, selector.Select(0).Failure);
        world.ApplyDamage(Id, 100, 0, 0);
        Assert.Equal(AiFailure.InvalidState, selector.Select(100).Failure);
    }

    [Fact]
    public void TickRejectionIsAtomicAndSameTickInputsKeepTheirOrder()
    {
        var world = Make();
        var brain = Brain(world);
        world.ApplyDamage(Id, 1, 1, 0);
        world.ReduceTension(Id, 0.1, 0);
        brain.Tick(0);
        int count = world.StateEvents.Count;
        var before = State(world);
        Assert.Equal(AiFailure.InvalidTick, world.ApplyDamage(Id, 1, 1, -1).Failure);
        Assert.Equal(AiFailure.InvalidTick, world.ReduceTension(Id, 0.1, -1).Failure);
        Assert.Equal(AiFailure.InvalidTick, brain.Tick(2));
        Assert.Same(before, State(world));
        Assert.Equal(count, world.StateEvents.Count);
        Assert.Equal(AiFailure.None, brain.Tick(1));
        Assert.Equal(new[] { PuppetInputKind.Initialize, PuppetInputKind.ApplyDamage,
            PuppetInputKind.ReduceTension, PuppetInputKind.RecoverPosture },
            world.StateEvents.Where(e => e.Kind == PuppetStateEventKind.Input).Select(e => e.Cause.Kind));
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue - 60)]
    public void GroggyDeadlineHandlesSignedTickExtremes(long start)
    {
        var world = new World();
        world.Add(Agent(), new PuppetInitialState(PuppetKind.Humanized));
        var brain = Brain(world);
        world.ReduceTension(Id, 1, start);
        for (int elapsed = 0; elapsed <= 60; elapsed++)
        {
            Assert.Equal(AiFailure.None, brain.Tick(start + elapsed));
            Assert.Equal(elapsed < 60 ? PuppetStatus.Groggy : PuppetStatus.Active, State(world).Status);
        }
    }

    [Fact]
    public void CustomSettingsAndInitialZeroValuesUseTheSameTransitions()
    {
        var settings = new PuppetStateSettings(groggyTicks: 3, reconnectTension: 0.7);
        var world = Make(PuppetKind.Humanized, settings, tension: 0);
        var brain = Brain(world);
        Assert.Equal(PuppetStatus.Groggy, State(world).Status);
        for (int tick = 0; tick < 3; tick++) brain.Tick(tick);
        Assert.Equal(PuppetStatus.Groggy, State(world).Status);
        brain.Tick(3);
        Assert.Equal(PuppetStatus.Active, State(world).Status);
        Assert.Equal(0.7, State(world).Tension);
        Assert.Equal(PuppetStatus.Downed, State(Make(PuppetKind.Humanized, health: 0, tension: 0)).Status);
        Assert.Equal(PuppetStatus.Broken, State(Make(health: 0)).Status);
        Assert.Equal(PuppetStatus.Released, State(Make(tension: 0)).Status);
    }

    [Fact]
    public void InvalidInitialValuesAndSettingsRejectWithoutAddingAnAgent()
    {
        foreach (double bad in new[] { -1, double.NaN, double.PositiveInfinity, 101 })
        {
            var world = new World();
            Assert.False(world.Add(Agent(), new PuppetInitialState(values:
                new Dictionary<PuppetValueId, double> { [PuppetValueId.Posture] = bad })).Success);
            Assert.Empty(world.GetAgents());
        }
        Assert.False(new World(new PuppetStateSettings(groggyTicks: 0)).Add(Agent()).Success);
        Assert.False(new World().Add(Agent(), new PuppetInitialState((PuppetKind)9)).Success);
    }

    [Fact]
    public void SameSeedAndInputsProduceSameStatesEventsAndTrajectories()
    {
        string[] Run()
        {
            var world = Make(PuppetKind.Humanized);
            world.Add(Agent(11, TargetRole.Guard, -3));
            world.Add(Agent(1, x: 8, side: Side.Players));
            var brain = PuppetBrain.Create(world, new PuppetSettings(), new DeterministicRandom(745)).Value;
            var trace = new List<string>();
            for (int tick = 0; tick < 180; tick++)
            {
                if (tick == 2) world.ApplyDamage(Id, 1, 80, tick);
                if (tick == 10 || tick == 80) world.ReduceTension(Id, 1, tick);
                if (tick == 90) world.ApplyDamage(Id, 100, 0, tick);
                Assert.Equal(AiFailure.None, brain.Tick(tick));
                foreach (var a in world.GetAgents()) trace.Add($"{a.Id.Value}:{a.Position.X:R}:{a.Position.Z:R}:{a.CurrentAction}:{a.Target?.Value}");
                foreach (var s in world.GetPuppetStates()) trace.Add($"{s.Id.Value}:{s.Kind}:{s.Status}:{s.Health:R}:{s.Posture:R}:{s.Tension:R}");
            }
            trace.AddRange(world.StateEvents.Select(e => $"{e.Tick}:{e.AgentId.Value}:{e.Kind}:{e.ValueId}:{e.Cause.Sequence}:{e.Cause.Kind}:{e.Before.Status}:{e.After.Status}:{e.After.Posture:R}"));
            return trace.ToArray();
        }
        Assert.Equal(Run(), Run());
    }
}
