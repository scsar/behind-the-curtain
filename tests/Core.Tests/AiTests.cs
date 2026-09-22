using System;
using System.Linq;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class AiTests
{
    private static readonly AgentId Id = new(1);
    private static AgentState Agent(int id = 1, Side side = Side.Puppets, bool human = false) =>
        new(new AgentId(id), side, human, TargetRole.Guard, new Vec2(2, 3), new Vec2(0, 1), 0.5);
    private static World World()
    {
        var world = new World();
        Assert.True(world.Add(Agent()).Success);
        return world;
    }
    private static UtilityAction Action(string id, Func<UtilityContext, double> input, double weight = 1) =>
        new(id, new[] { new UtilityConsideration("value", input, new ResponseCurve(ResponseCurveKind.Linear, 1, 0)) }, weight);
    private static UtilitySelector Selector(World world, UtilityAction[] actions, long interval = 1, double bonus = 0, ulong seed = 42)
    {
        var result = UtilitySelector.Create(world, Id, actions, new UtilitySettings(interval, bonus), new DeterministicRandom(seed));
        Assert.True(result.Success, result.Failure.ToString());
        return result.Value;
    }

    [Fact]
    public void Random_HasFixedSplitMix64VectorAndRepeatableMixedSequence()
    {
        var known = new DeterministicRandom(0);
        Assert.Equal((0xE220A8397B1DCDAFUL >> 11) / 9007199254740992.0, known.NextDouble());
        Assert.Equal((0x6E789E6AA1B965F4UL >> 11) / 9007199254740992.0, known.NextDouble());
        var a = new DeterministicRandom(123);
        var b = new DeterministicRandom(123);
        for (int i = 0; i < 1000; i++)
        {
            var value = a.NextDouble();
            Assert.Equal(value, b.NextDouble());
            Assert.True(value >= 0 && value < 1);
            var integer = a.NextInt(int.MinValue, int.MaxValue);
            Assert.Equal(integer.Value, b.NextInt(int.MinValue, int.MaxValue).Value);
            Assert.InRange(integer.Value, int.MinValue, int.MaxValue - 1);
            Assert.Equal(-3, a.NextInt(-3, -2).Value);
            b.NextInt(-3, -2);
        }
        Assert.Equal(AiFailure.InvalidRange, a.NextInt(3, 3).Failure);
        Assert.Equal(AiFailure.InvalidRange, a.NextInt(4, 3).Failure);
        Assert.Equal(b.NextDouble(), a.NextDouble());
    }

    [Fact]
    public void World_StoresSnapshotsAndSeparatesSides()
    {
        var world = World();
        Assert.True(world.Add(Agent(2, Side.Players, true)).Success);
        var before = world.Get(Id).Value;
        var moved = world.UpdatePosition(Id, new Vec2(-4, 8), new Vec2(1, 0));
        Assert.True(moved.Success);
        Assert.Equal(2, before.Position.X);
        Assert.Equal(-4, moved.Value.Position.X);
        Assert.Equal(1, moved.Value.Direction.X);
        Assert.Equal(TargetRole.Guard, moved.Value.Role);
        Assert.Equal(0.5, moved.Value.Radius);
        Assert.True(world.Get(new AgentId(2)).Value.IsHuman);
        var action = world.UpdateAction(Id, "approach", new AgentId(2));
        Assert.Equal("approach", action.Value.CurrentAction);
        Assert.Equal(new AgentId(2), action.Value.Target);
        Assert.Null(world.UpdateAction(Id, null).Value.Target);
        Assert.Same(world.GetBlackboard(Side.Puppets).Value, world.GetBlackboard(Side.Puppets).Value);
        Assert.NotSame(world.GetBlackboard(Side.Puppets).Value, world.GetBlackboard(Side.Players).Value);
    }

    [Fact]
    public void World_RejectsInvalidInputsWithoutMutation()
    {
        var world = World();
        Assert.Equal(AiFailure.DuplicateAgent, world.Add(Agent()).Failure);
        Assert.Equal(AiFailure.InvalidId, world.Add(null).Failure);
        Assert.Equal(AiFailure.InvalidId, world.Get(default).Failure);
        Assert.Equal(AiFailure.AgentNotFound, world.Get(new AgentId(99)).Failure);
        Assert.Equal(AiFailure.InvalidSide, world.Add(Agent(2, (Side)99)).Failure);
        Assert.Equal(AiFailure.InvalidSide, world.GetBlackboard((Side)99).Failure);
        Assert.Equal(AiFailure.InvalidPosition, world.UpdatePosition(Id, new Vec2(double.NaN, 0), new Vec2(1, 0)).Failure);
        Assert.Equal(AiFailure.InvalidDirection, world.UpdatePosition(Id, default, default).Failure);
        Assert.Equal(AiFailure.InvalidDirection, world.UpdatePosition(Id, default, new Vec2(double.PositiveInfinity, 0)).Failure);
        Assert.Equal(AiFailure.InvalidRadius, world.Add(new AgentState(new AgentId(2), Side.Puppets,
            false, TargetRole.Guard, default, new Vec2(1, 0), -1)).Failure);
        Assert.Equal(AiFailure.InvalidRole, world.Add(new AgentState(new AgentId(2), Side.Puppets,
            false, (TargetRole)99, default, new Vec2(1, 0), 1)).Failure);
        Assert.Equal(AiFailure.InvalidAction, world.UpdateAction(Id, " ").Failure);
        Assert.Equal(AiFailure.TargetNotFound, world.UpdateAction(Id, "hit", new AgentId(99)).Failure);
        Assert.Null(world.Get(Id).Value.CurrentAction);
        Assert.Equal(2, world.Get(Id).Value.Position.X);
    }

    [Fact]
    public void Blackboard_ReservationsConflictRenewExpireAndRejectTimeReversal()
    {
        var board = new Blackboard();
        var other = new AgentId(2);
        Assert.True(board.Reserve("guard", Id, 10, 0).Success);
        Assert.Equal(AiFailure.ReservationConflict, board.Reserve("guard", other, 20, 10).Failure);
        Assert.Equal(Id, board.GetReservation("guard", 10).Value.Owner);
        Assert.True(board.Reserve("guard", other, 20, 11).Success);
        Assert.True(board.Reserve("guard", other, 30, 12).Success);
        Assert.True(board.GetReservation("guard", 30).Success);
        Assert.Equal(AiFailure.ReservationNotFound, board.GetReservation("guard", 31).Failure);
        Assert.Equal(AiFailure.InvalidTick, board.GetReservation("guard", 30).Failure);
        Assert.Equal(AiFailure.InvalidTick, board.Reserve("guard", Id, 50, 30).Failure);
        Assert.Equal(AiFailure.InvalidTick, board.Reserve("guard", Id, 31, 32).Failure);
        Assert.Equal(AiFailure.InvalidKey, board.Reserve("", Id, 50, 32).Failure);
        Assert.Equal(AiFailure.InvalidId, board.Reserve("guard", default, 50, 32).Failure);
        Assert.True(board.Reserve("last", Id, long.MaxValue, long.MaxValue).Success);
        Assert.True(board.GetReservation("last", long.MaxValue).Success);
    }

    [Fact]
    public void Blackboard_FactsAndIntentionsReturnFailureReasons()
    {
        var board = new Blackboard();
        Assert.Equal(AiFailure.None, board.SetFact("position", new Vec2(3, 4)));
        Assert.Equal(4, board.GetFact<Vec2>("position").Value.Z);
        Assert.Equal(AiFailure.InvalidValue, board.GetFact<int>("position").Failure);
        Assert.Equal(AiFailure.FactNotFound, board.GetFact<int>("missing").Failure);
        Assert.Equal(AiFailure.InvalidKey, board.SetFact(" ", 1));
        Assert.Equal(AiFailure.InvalidValue, board.SetFact<string>("null", null!));
        Assert.Equal(1, board.GetIntention("idle").Value);
        Assert.Equal(AiFailure.InvalidWeight, board.SetIntention("idle", double.NaN));
        Assert.Equal(AiFailure.InvalidWeight, board.SetIntention("idle", -1));
        Assert.Equal(AiFailure.InvalidKey, board.SetIntention("", 1));
    }

    [Theory]
    [InlineData(ResponseCurveKind.Linear, 2, -0.5, 0, 0.5, 0.5)]
    [InlineData(ResponseCurveKind.Linear, 2, 0, 0, 1, 1)]
    [InlineData(ResponseCurveKind.Linear, 1, -2, 0, 0, 0)]
    [InlineData(ResponseCurveKind.Quadratic, 2, 1, -0.5, 0.5, 0.5)]
    [InlineData(ResponseCurveKind.Logistic, 8, 0.5, 0, 0.5, 0.5)]
    [InlineData(ResponseCurveKind.Step, 0.5, 1, 0, 0.5, 1)]
    [InlineData(ResponseCurveKind.Step, 0.5, 1, 0, 0.49, 0)]
    public void Curves_UseParametersAndClamp(ResponseCurveKind kind, double a, double b, double c, double input, double expected)
    {
        Assert.Equal(expected, new ResponseCurve(kind, a, b, c).Evaluate(input).Value, 12);
    }

    [Fact]
    public void SameSeedAndInputs_ProduceSameNontrivialSelectionSequence()
    {
        string?[] Run()
        {
            var world = World();
            var selector = Selector(world, new[] { Action("a", _ => 1), Action("b", _ => 1) });
            return Enumerable.Range(0, 100).Select(t => selector.Select(t).Value.ActionId).ToArray();
        }
        var first = Run();
        Assert.Equal(first, Run());
        Assert.Contains("a", first);
        Assert.Contains("b", first);
    }

    [Fact]
    public void ZeroConsideration_ExcludesCurrentActionAndStillReportsAllFactors()
    {
        var world = World();
        world.UpdateAction(Id, "blocked");
        var blocked = new UtilityAction("blocked", new[] {
            new UtilityConsideration("zero", _ => 0, new ResponseCurve(ResponseCurveKind.Linear, 1, 0)),
            new UtilityConsideration("later", _ => 0.8, new ResponseCurve(ResponseCurveKind.Linear, 1, 0)) }, 10);
        var result = Selector(world, new[] { blocked, Action("valid", _ => 0.1) }, bonus: 100).Select(0).Value;
        Assert.Equal("valid", result.ActionId);
        Assert.False(result.Scores[0].Eligible);
        Assert.Equal(0, result.Scores[0].PersistenceBonus);
        Assert.Equal(0.8, result.Scores[0].Considerations[1].CurveValue);
        Assert.Null(Selector(world, new[] { blocked }, bonus: 100).Select(0).Value.ActionId);
        Assert.Null(Selector(world, Array.Empty<UtilityAction>()).Select(0).Value.ActionId);
    }

    [Fact]
    public void PersistenceMaintainsCloseScoresButAllowsLargeDifference()
    {
        var world = World();
        world.UpdateAction(Id, "a");
        double challenger = 0.55;
        var selector = Selector(world, new[] { Action("a", _ => 0.5), Action("b", _ => challenger) }, bonus: 0.1);
        var first = selector.Select(0).Value;
        Assert.Equal("a", first.ActionId);
        Assert.Equal(0.6, first.Scores[0].Score, 12);
        challenger = 0.8;
        Assert.Equal("b", selector.Select(1).Value.ActionId);
    }

    [Fact]
    public void IntentionsMultiplyProductAndZeroCannotReceiveBonus()
    {
        var world = World();
        var board = world.GetBlackboard(Side.Puppets).Value;
        var selector = Selector(world, new[] { Action("a", _ => 0.5, 2), Action("b", _ => 0.6) }, bonus: 10);
        Assert.Equal("a", selector.Select(0).Value.ActionId);
        board.SetIntention("b", 2);
        var changed = selector.Select(1).Value;
        Assert.Equal("b", changed.ActionId);
        Assert.Equal(1.2, changed.Scores[1].Score, 12);
        Assert.Equal(2, changed.Scores[1].IntentionMultiplier);
        world.UpdateAction(Id, "b");
        board.SetIntention("b", 0);
        Assert.Equal("a", selector.Select(2).Value.ActionId);
    }

    [Fact]
    public void ReevaluationInterval_CachesTraceDoesNotReadInputAndRejectsReversal()
    {
        var world = World();
        int calls = 0;
        double input = 1;
        var selector = Selector(world, new[] { Action("a", _ => { calls++; return input; }) }, interval: 6);
        var first = selector.Select(-2).Value;
        input = 0;
        var cached = selector.Select(3).Value;
        Assert.False(cached.Reevaluated);
        Assert.Equal(-2, cached.EvaluatedAtTick);
        Assert.Same(first.Scores, cached.Scores);
        Assert.Equal(1, calls);
        Assert.Equal(AiFailure.InvalidTick, selector.Select(2).Failure);
        var next = selector.Select(4).Value;
        Assert.True(next.Reevaluated);
        Assert.Null(next.ActionId);
        Assert.Equal(2, calls);
        var extreme = Selector(world, new[] { Action("a", _ => 1) }, interval: long.MaxValue);
        extreme.Select(long.MinValue);
        Assert.True(extreme.Select(long.MaxValue).Value.Reevaluated);
    }

    [Fact]
    public void MultipleConsiderationsMultiplyAndTraceKeepsRawInputs()
    {
        var world = World();
        world.GetBlackboard(Side.Puppets).Value.SetIntention("a", 3);
        world.UpdateAction(Id, "a");
        var action = new UtilityAction("a", new[] {
            new UtilityConsideration("distance", _ => 0.5, new ResponseCurve(ResponseCurveKind.Quadratic, 1, 0)),
            new UtilityConsideration("threat", _ => 0.8, new ResponseCurve(ResponseCurveKind.Linear, 1, 0)) }, 2);
        var score = Selector(world, new[] { action }, bonus: 0.1).Select(0).Value.Scores[0];
        Assert.Equal(0.5, score.Considerations[0].Input);
        Assert.Equal(0.25, score.Considerations[0].CurveValue);
        Assert.Equal(0.2, score.Product, 12);
        Assert.Equal(1.3, score.Score, 12);
    }

    [Fact]
    public void CachedCallsDoNotConsumeTieBreakRandomness()
    {
        var actions = new[] { Action("a", _ => 1), Action("b", _ => 1) };
        var a = Selector(World(), actions, interval: 6);
        var b = Selector(World(), actions, interval: 6);
        for (int tick = 0; tick < 60; tick += 6)
        {
            var evaluated = a.Select(tick).Value;
            Assert.Equal(b.Select(tick).Value.ActionId, evaluated.ActionId);
            Assert.Equal(evaluated.ActionId, a.Select(tick + 1).Value.ActionId);
            Assert.False(a.Select(tick + 5).Value.Reevaluated);
        }
    }

    [Fact]
    public void InvalidDefinitionsInputsAndScores_ReturnReasonsAndCanRetry()
    {
        var world = World();
        var rng = new DeterministicRandom(0);
        var action = Action("a", _ => 1);
        Assert.Equal(AiFailure.InvalidSettings, UtilitySelector.Create(world, Id, new[] { action }, new UtilitySettings(0), rng).Failure);
        Assert.Equal(AiFailure.DuplicateAction, UtilitySelector.Create(world, Id, new[] { action, action }, new UtilitySettings(), rng).Failure);
        Assert.Equal(AiFailure.InvalidWeight, UtilitySelector.Create(world, Id, new[] { Action("a", _ => 1, double.PositiveInfinity) }, new UtilitySettings(), rng).Failure);
        Assert.Equal(AiFailure.InvalidCurve, new ResponseCurve((ResponseCurveKind)99, 1, 0).Evaluate(0.5).Failure);
        Assert.Equal(AiFailure.InvalidInput, new ResponseCurve(ResponseCurveKind.Linear, 1, 0).Evaluate(1.1).Failure);
        double input = double.NaN;
        var selector = Selector(world, new[] { Action("a", _ => input) });
        Assert.Equal(AiFailure.InvalidInput, selector.Select(0).Failure);
        input = 1;
        Assert.Equal("a", selector.Select(0).Value.ActionId);
        Assert.Equal(AiFailure.InvalidInput, Selector(world, new[] { Action("a", _ => throw new InvalidOperationException()) }).Select(0).Failure);
        world.GetBlackboard(Side.Puppets).Value.SetIntention("a", double.MaxValue);
        Assert.Equal(AiFailure.InvalidWeight, Selector(world, new[] { Action("a", _ => 1, 2) }).Select(0).Failure);
    }
}
