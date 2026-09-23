using System;
using System.Linq;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class PuppetBrainTests
{
    private const ulong Seed = 1514;
    private const int N = 120;

    [Fact]
    public void LearnedHealerThreatIsInterceptedAndGuardFollowsLateralMovement()
    {
        var world = LearnedWorld(TargetRole.Healer, out var tactic);
        Assert.Equal(TacticId.GuardHealer, tactic.Id);
        Add(world, 1, new Vec2(6, 0), Side.Players);
        Add(world, 10, new Vec2(-4, -4), role: TargetRole.Guard);
        Add(world, 11, new Vec2(0, 0), role: TargetRole.Healer);
        var brain = Brain(world);
        Run(brain, 0, N);
        AssertBetween(world, 10, 11, 1);
        Assert.Equal(ActionIds.Intercept, world.Get(new AgentId(10)).Value.CurrentAction);
        Assert.Equal(new AgentId(1), world.Get(new AgentId(10)).Value.Target);
        var first = brain.GetDecision(new AgentId(10)).Value.Goal;
        // The healer now maintains distance behind its guard, so the intercept point is relative.
        Assert.InRange(Distance(first, world.Get(new AgentId(11)).Value.Position), 1.3, 1.7);
        world.UpdatePosition(new AgentId(1), new Vec2(0, 6), new Vec2(0, 1));
        Run(brain, N, N);
        AssertBetween(world, 10, 11, 1);
        var second = brain.GetDecision(new AgentId(10)).Value.Goal;
        Assert.InRange(Distance(second, world.Get(new AgentId(11)).Value.Position), 1.3, 1.7);
        Assert.NotEqual(first, second);
        Assert.Equal(ActionIds.Intercept, world.Get(new AgentId(10)).Value.CurrentAction);
    }

    [Fact]
    public void ShieldAdvanceClosesDistanceToRangedThreatRatherThanNearestHuman()
    {
        var world = LearnedWorld(TargetRole.Guard, out var tactic, range: AttackRange.Ranged);
        Assert.Equal(TacticId.ShieldAdvance, tactic.Id);
        Add(world, 1, new Vec2(8, 0), Side.Players);
        Add(world, 2, new Vec2(-4, 8), Side.Players);
        Add(world, 10, new Vec2(-6, -3), role: TargetRole.Guard);
        Add(world, 11, new Vec2(-6, 0));
        Add(world, 12, new Vec2(-6, 3), role: TargetRole.Healer);
        var start = world.GetAgents().Where(a => a.Side == Side.Puppets && a.Role != TargetRole.Healer).ToArray();
        var brain = Brain(world);
        Run(brain, 0, 1);
        foreach (var agent in start)
        {
            Assert.Equal(ActionIds.Approach, world.Get(agent.Id).Value.CurrentAction);
            Assert.Equal(new AgentId(1), world.Get(agent.Id).Value.Target);
        }
        Run(brain, 1, N);
        foreach (var agent in start)
            Assert.True(Distance(world.Get(agent.Id).Value.Position, new Vec2(8, 0))
                < Distance(agent.Position, new Vec2(8, 0)) - 5);
    }

    [Fact]
    public void SpreadFormationIncreasesMinimumPuppetDistance()
    {
        var world = LearnedWorld(TargetRole.Guard, out var tactic, area: 2);
        Assert.Equal(TacticId.SpreadFormation, tactic.Id);
        Add(world, 10, new Vec2(-0.5, -0.5));
        Add(world, 11, new Vec2(0.5, -0.5));
        Add(world, 12, new Vec2(-0.5, 0.5));
        Add(world, 13, new Vec2(0.5, 0.5));
        double initial = MinimumDistance(world);
        var brain = Brain(world);
        Run(brain, 0, N);
        Assert.True(MinimumDistance(world) > initial + 1);
        Assert.All(world.GetAgents(), a => Assert.Null(a.Target));
    }

    [Fact]
    public void StandardFormationHoldsAndReturnsToInitialWorldPositionsWithoutThreats()
    {
        var world = new World();
        Add(world, 10, new Vec2(-4, 0), role: TargetRole.Guard);
        Add(world, 11, new Vec2(0, 0), role: TargetRole.Guard);
        Add(world, 12, new Vec2(4, 0), role: TargetRole.Guard);
        // Guards hold only outside the threat radius; attackers now engage at any distance.
        Add(world, 1, new Vec2(0, 9), Side.Players);
        var start = world.GetAgents().Where(a => a.Side == Side.Puppets).ToArray();
        var brain = Brain(world);
        Run(brain, 0, N);
        foreach (var a in start)
        {
            Assert.Equal(a.Position, world.Get(a.Id).Value.Position);
            Assert.Equal(ActionIds.Hold, world.Get(a.Id).Value.CurrentAction);
        }
        world.UpdatePosition(new AgentId(10), new Vec2(-4, -2), new Vec2(0, -1));
        Run(brain, N, N);
        Assert.Equal(new Vec2(-4, 0), world.Get(new AgentId(10)).Value.Position);
    }

    [Fact]
    public void EveryTickIsDeterministicIncludingPositionsActionsTargetsGoalsAndScores()
    {
        var a = ScenarioWorld(false);
        var b = ScenarioWorld(true); // insertion order does not affect simulation
        var first = Brain(a);
        var second = Brain(b);
        for (int tick = 0; tick < 240; tick++)
        {
            var player = new Vec2(7 * Math.Cos(tick / 40.0), 7 * Math.Sin(tick / 40.0));
            a.UpdatePosition(new AgentId(1), player, new Vec2(0, 1));
            b.UpdatePosition(new AgentId(1), player, new Vec2(0, 1));
            Assert.Equal(AiFailure.None, first.Tick(tick));
            Assert.Equal(AiFailure.None, second.Tick(tick));
            foreach (var state in a.GetAgents().Where(x => x.Side == Side.Puppets))
            {
                var other = b.Get(state.Id).Value;
                Assert.Equal(state.Position, other.Position);
                Assert.Equal(state.Direction, other.Direction);
                Assert.Equal(state.CurrentAction, other.CurrentAction);
                Assert.Equal(state.Target, other.Target);
                var x = first.GetDecision(state.Id).Value;
                var y = second.GetDecision(state.Id).Value;
                Assert.Equal(x.Goal, y.Goal);
                Assert.Equal(x.Selection.Scores.Select(s => s.Score), y.Selection.Scores.Select(s => s.Score));
            }
        }
    }

    [Theory]
    [InlineData(30, 30)]
    [InlineData(-30, 30)]
    [InlineData(30, -30)]
    [InlineData(-30, -30)]
    public void PuppetsStayInsideArenaWhileChasingExternalPlayers(double x, double z)
    {
        var world = new World();
        Add(world, 1, new Vec2(x, z), Side.Players);
        Add(world, 10, new Vec2(0, 0));
        world.GetBlackboard(Side.Puppets).Value.SetIntention(ActionIds.Approach, 20);
        var brain = Brain(world);
        for (int tick = 0; tick < 400; tick++)
        {
            Run(brain, tick, 1);
            var position = world.Get(new AgentId(10)).Value.Position;
            var goal = brain.GetDecision(new AgentId(10)).Value.Goal;
            Assert.InRange(position.X, -10, 10);
            Assert.InRange(position.Z, -10, 10);
            Assert.InRange(goal.X, -10, 10);
            Assert.InRange(goal.Z, -10, 10);
        }
        Assert.Equal(Math.Sign(x) * 10, world.Get(new AgentId(10)).Value.Position.X);
        Assert.Equal(Math.Sign(z) * 10, world.Get(new AgentId(10)).Value.Position.Z);
        Assert.Equal(new Vec2(x, z), world.Get(new AgentId(1)).Value.Position);
    }

    [Fact]
    public void AttackRequiresHumanInRangeAndDoesNotMoveOrDamage()
    {
        var world = new World();
        Add(world, 1, new Vec2(2, 0), Side.Players);
        Add(world, 2, new Vec2(0.1, 0), Side.Players, human: false);
        Add(world, 10, new Vec2(0, 0));
        var brain = Brain(world);
        Run(brain, 0, 1);
        var agent = world.Get(new AgentId(10)).Value;
        Assert.Equal(ActionIds.Attack, agent.CurrentAction);
        Assert.Equal(new AgentId(1), agent.Target);
        Assert.Equal(new Vec2(0, 0), agent.Position);
        Assert.Equal(agent.Position, brain.GetDecision(agent.Id).Value.Goal);
        world.UpdatePosition(new AgentId(1), new Vec2(2.001, 0), new Vec2(1, 0));
        Run(brain, 1, 30);
        Assert.False(Score(brain, 10, ActionIds.Attack).Eligible);
        Assert.Equal(ActionIds.Approach, world.Get(agent.Id).Value.CurrentAction);
    }

    [Fact]
    public void StationaryAttackFacesHumanMovingBehindOnNextTick()
    {
        var world = new World();
        Add(world, 1, new Vec2(0, 1), Side.Players);
        Add(world, 10, new Vec2(0, 0));
        var brain = Brain(world);
        Run(brain, 0, 1);
        var before = world.Get(new AgentId(10)).Value;
        Assert.Equal(ActionIds.Attack, before.CurrentAction);
        Assert.Equal(new Vec2(0, 1), before.Direction);

        var player = new Vec2(0.6, -0.8);
        Assert.True(world.UpdatePosition(new AgentId(1), player, new Vec2(0, 1)).Success);
        Run(brain, 1, 1);
        var after = world.Get(before.Id).Value;
        Assert.Equal(ActionIds.Attack, after.CurrentAction);
        Assert.Equal(new AgentId(1), after.Target);
        Assert.Equal(before.Position, after.Position);
        // Facing must update even between action reevaluations.
        Assert.False(brain.GetDecision(after.Id).Value.Selection.Reevaluated);
        var distance = Distance(after.Position, player);
        var dot = (after.Direction.X * (player.X - after.Position.X)
            + after.Direction.Z * (player.Z - after.Position.Z)) / distance;
        Assert.True(dot >= 0.99, $"Facing dot product: {dot}");
    }

    [Fact]
    public void MovingAwayFromTargetFacesMovementInsteadOfTarget()
    {
        var world = new World();
        Add(world, 1, new Vec2(0, 0), Side.Players);
        Add(world, 10, new Vec2(1, 1), role: TargetRole.Healer);
        var brain = Brain(world);
        var before = world.Get(new AgentId(10)).Value;
        Run(brain, 0, 1);
        var after = world.Get(before.Id).Value;
        Assert.Equal(ActionIds.KeepDistance, after.CurrentAction);
        Assert.Equal(new AgentId(1), after.Target);
        var distance = Distance(before.Position, after.Position);
        Assert.True(distance > 0);
        Assert.Equal((after.Position.X - before.Position.X) / distance, after.Direction.X, 12);
        Assert.Equal((after.Position.Z - before.Position.Z) / distance, after.Direction.Z, 12);
        Assert.True(after.Direction.X * -after.Position.X + after.Direction.Z * -after.Position.Z < 0);
    }

    [Fact]
    public void StationaryPuppetWithoutTargetPreservesPreviousDirection()
    {
        var world = new World();
        Add(world, 10, new Vec2(0, 0));
        var direction = new Vec2(-0.6, -0.8);
        Assert.True(world.UpdatePosition(new AgentId(10), new Vec2(0, 0), direction).Success);
        var brain = Brain(world);
        for (int tick = 0; tick < 30; tick++)
        {
            Run(brain, tick, 1);
            var agent = world.Get(new AgentId(10)).Value;
            Assert.Null(brain.GetDecision(agent.Id).Value.Target);
            Assert.Equal(new Vec2(0, 0), agent.Position);
            Assert.Equal(direction, agent.Direction);
        }
    }

    [Theory]
    [InlineData(false, true, TargetRole.Guard)]
    [InlineData(true, false, TargetRole.Guard)]
    [InlineData(true, true, TargetRole.Attacker)]
    public void InterceptRequiresGuardHealerAndValidHumanThreat(bool fact, bool healer, TargetRole role)
    {
        var world = new World();
        Add(world, 1, new Vec2(7, 0), Side.Players);
        Add(world, 10, new Vec2(-4, 0), role: role);
        if (healer) Add(world, 11, new Vec2(0, 0), role: TargetRole.Healer);
        if (fact) world.GetBlackboard(Side.Puppets).Value.SetFact(TargetFactIds.HealerThreat, new AgentId(1));
        var brain = Brain(world);
        Run(brain, 0, 1);
        Assert.False(Score(brain, 10, ActionIds.Intercept).Eligible);
    }

    [Theory]
    [InlineData(99)] // not in world
    [InlineData(2)] // player bot
    [InlineData(11)] // puppet
    public void InvalidThreatFactsFallBackForApproachAndDisableIntercept(int threat)
    {
        var world = new World();
        Add(world, 1, new Vec2(8, 0), Side.Players);
        Add(world, 2, new Vec2(0, 1), Side.Players, human: false);
        Add(world, 10, new Vec2(0, 0), role: TargetRole.Guard);
        Add(world, 11, new Vec2(-4, 0), role: TargetRole.Healer);
        var board = world.GetBlackboard(Side.Puppets).Value;
        board.SetFact(TargetFactIds.RangedThreat, new AgentId(threat));
        board.SetFact(TargetFactIds.HealerThreat, new AgentId(threat));
        board.SetIntention(ActionIds.Approach, 10);
        var brain = Brain(world);
        Run(brain, 0, 1);
        Assert.False(Score(brain, 10, ActionIds.Intercept).Eligible);
        Assert.Equal(new AgentId(1), world.Get(new AgentId(10)).Value.Target);
    }

    [Fact]
    public void CoincidentPuppetsSeparateAndDirectionsMatchActualMovement()
    {
        var world = new World();
        Add(world, 10, new Vec2(0, 0));
        Add(world, 11, new Vec2(0, 0));
        world.GetBlackboard(Side.Puppets).Value.SetIntention(ActionIds.Spread, 2);
        world.GetBlackboard(Side.Puppets).Value.SetIntention(ActionIds.Hold, 0.7);
        var brain = Brain(world);
        Run(brain, 0, 1);
        Assert.Equal(-0.1, world.Get(new AgentId(10)).Value.Position.X, 12);
        Assert.Equal(0.1, world.Get(new AgentId(11)).Value.Position.X, 12);
        Assert.Equal(new Vec2(-1, 0), world.Get(new AgentId(10)).Value.Direction);
        Assert.Equal(new Vec2(1, 0), world.Get(new AgentId(11)).Value.Direction);
        Run(brain, 1, N);
        Assert.True(MinimumDistance(world) > 2);
    }

    [Fact]
    public void DiagnosticsExposeEveryCandidateAndAreImmutableSnapshots()
    {
        var world = ScenarioWorld(false);
        var brain = Brain(world);
        Assert.False(brain.GetDecision(new AgentId(10)).Success);
        Run(brain, 0, 1);
        var snapshot = brain.GetDecision(new AgentId(10)).Value;
        Assert.Equal(new[] { ActionIds.Approach, ActionIds.Intercept, ActionIds.Spread, ActionIds.Hold, ActionIds.Attack, ActionIds.KeepDistance },
            snapshot.Selection.Scores.Select(s => s.ActionId));
        Assert.Single(Score(brain, 10, ActionIds.Intercept).Considerations);
        foreach (var score in snapshot.Selection.Scores)
        {
            Assert.All(score.Considerations, c => Assert.InRange(c.Input, 0, 1));
            Assert.All(score.Considerations, c => Assert.InRange(c.CurveValue, 0, 1));
            Assert.Equal(score.Product * score.BaseWeight * score.IntentionMultiplier + score.PersistenceBonus, score.Score);
        }
        Run(brain, 1, 1);
        Assert.Equal(0, snapshot.Selection.EvaluatedAtTick);
        Assert.Equal(0, brain.GetDecision(new AgentId(10)).Value.Selection.EvaluatedAtTick);
        Assert.False(brain.GetDecision(new AgentId(10)).Value.Selection.Reevaluated);
    }

    [Fact]
    public void CustomSpeedUsesIntegerTickRateAndInvalidTicksDoNotMoveAgents()
    {
        var world = new World();
        Add(world, 1, new Vec2(9, 0), Side.Players);
        Add(world, 10, new Vec2(0, 0));
        world.GetBlackboard(Side.Puppets).Value.SetIntention(ActionIds.Approach, 10);
        var brain = Brain(world, new PuppetSettings(moveSpeed: 4, ticksPerSecond: 20));
        Run(brain, 50, 1);
        var position = world.Get(new AgentId(10)).Value.Position;
        Assert.Equal(0.2, position.X, 12);
        foreach (long tick in new long[] { 50, 49, 52, long.MinValue, long.MaxValue })
        {
            Assert.Equal(AiFailure.InvalidTick, brain.Tick(tick));
            Assert.Equal(position, world.Get(new AgentId(10)).Value.Position);
        }
        Run(brain, 51, 1);
        Assert.Equal(0.4, world.Get(new AgentId(10)).Value.Position.X, 12);
    }

    [Fact]
    public void InvalidSettingsAreRejected()
    {
        var world = new World();
        foreach (var settings in new[] { new PuppetSettings(moveSpeed: -1), new PuppetSettings(ticksPerSecond: 0),
            new PuppetSettings(arenaHalfSize: double.NaN), new PuppetSettings(attackRange: 0),
            new PuppetSettings(spreadDistance: double.PositiveInfinity), new PuppetSettings(holdWeight: 0),
            new PuppetSettings(interceptPositionFloor: 2), new PuppetSettings(persistenceBonus: -1) })
            Assert.Equal(AiFailure.InvalidSettings, PuppetBrain.Create(world, settings, new DeterministicRandom(Seed)).Failure);
    }

    private static World LearnedWorld(TargetRole target, out Tactic tactic, int area = 0, AttackRange range = AttackRange.Melee)
    {
        var expedition = SuspicionTests.Create();
        SuspicionTests.Complete(expedition, SuspicionTests.Observe(1, target, area, range));
        Assert.True(expedition.TryStartEncounter(out tactic));
        return expedition.EncounterWorld!;
    }
    private static World ScenarioWorld(bool reverse)
    {
        var world = new World();
        var ids = new[] { 1, 10, 11, 12, 13 };
        foreach (var id in reverse ? ids.Reverse() : ids)
            Add(world, id, id == 1 ? new Vec2(7, 0) : new Vec2(id - 11, 0),
                id == 1 ? Side.Players : Side.Puppets,
                id == 10 ? TargetRole.Guard : id == 13 ? TargetRole.Healer : TargetRole.Attacker);
        var board = world.GetBlackboard(Side.Puppets).Value;
        board.SetFact(TargetFactIds.HealerThreat, new AgentId(1));
        board.SetFact(TargetFactIds.RangedThreat, new AgentId(1));
        board.SetIntention(ActionIds.Intercept, 2.5);
        board.SetIntention(ActionIds.Approach, 2);
        return world;
    }
    private static void Add(World world, int id, Vec2 position, Side side = Side.Puppets,
        TargetRole role = TargetRole.Attacker, bool human = true) =>
        Assert.True(world.Add(new AgentState(new AgentId(id), side, side == Side.Players && human,
            role, position, new Vec2(0, 1), 0.3)).Success);
    private static PuppetBrain Brain(World world, PuppetSettings? settings = null)
    {
        var result = PuppetBrain.Create(world, settings ?? new PuppetSettings(), new DeterministicRandom(Seed));
        Assert.True(result.Success);
        return result.Value;
    }
    private static void Run(PuppetBrain brain, int first, int count)
    {
        for (int tick = first; tick < first + count; tick++) Assert.Equal(AiFailure.None, brain.Tick(tick));
    }
    private static ActionScore Score(PuppetBrain brain, int id, string action) =>
        brain.GetDecision(new AgentId(id)).Value.Selection.Scores.Single(s => s.ActionId == action);
    private static double Distance(Vec2 a, Vec2 b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Z - b.Z, 2));
    private static double MinimumDistance(World world)
    {
        var agents = world.GetAgents().Where(a => a.Side == Side.Puppets).ToArray();
        return agents.SelectMany((a, i) => agents.Skip(i + 1).Select(b => Distance(a.Position, b.Position))).Min();
    }
    private static void AssertBetween(World world, int guard, int healer, int threat)
    {
        var p = world.Get(new AgentId(guard)).Value.Position;
        var a = world.Get(new AgentId(healer)).Value.Position;
        var b = world.Get(new AgentId(threat)).Value.Position;
        double dx = b.X - a.X, dz = b.Z - a.Z;
        double t = ((p.X - a.X) * dx + (p.Z - a.Z) * dz) / (dx * dx + dz * dz);
        Assert.InRange(t, 0, 1);
        Assert.InRange(Distance(p, new Vec2(a.X + t * dx, a.Z + t * dz)), 0, 0.5);
    }
}
