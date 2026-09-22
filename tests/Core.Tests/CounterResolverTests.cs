using System;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class CounterResolverTests
{
    [Theory]
    [InlineData(10L, InterferenceMethod.Cut)]
    [InlineData(15L, InterferenceMethod.Shoot)]
    [InlineData(20L, InterferenceMethod.Disrupt)]
    public void Resolve_AllowedMethodWithinInclusiveWindowBreaksAttack(long tick, InterferenceMethod method)
    {
        var point = new ConnectionPoint(method);
        var attack = Create(point);

        var result = CounterResolver.Resolve(attack, tick, method, point);

        Assert.True(result.Success);
        Assert.Equal(CounterFailureReason.None, result.FailureReason);
        Assert.Equal(JointAttackState.Broken, attack.State);
        Assert.Equal(tick, attack.CurrentTick);
        Assert.False(attack.Advance(30));
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(9L)]
    [InlineData(21L)]
    [InlineData(30L)]
    [InlineData(long.MaxValue)]
    public void Resolve_OutsideWindowDoesNotConsumeOpportunity(long tick)
    {
        var point = new ConnectionPoint(InterferenceMethod.Cut);
        var attack = Create(point);
        AssertFailure(attack, tick, InterferenceMethod.Cut, point, CounterFailureReason.OutsideWindow);
        Assert.True(CounterResolver.Resolve(attack, 10, InterferenceMethod.Cut, point).Success);
    }

    [Theory]
    [InlineData(InterferenceMethod.Shoot)]
    [InlineData(InterferenceMethod.Disrupt)]
    [InlineData((InterferenceMethod)0)]
    [InlineData((InterferenceMethod)8)]
    [InlineData(InterferenceMethod.Cut | InterferenceMethod.Shoot)]
    public void Resolve_WrongOrInvalidMethodDoesNotConsumeOpportunity(InterferenceMethod method)
    {
        var point = new ConnectionPoint(InterferenceMethod.Cut);
        var attack = Create(point);
        AssertFailure(attack, 10, method, point, CounterFailureReason.MethodMismatch);
        Assert.True(CounterResolver.Resolve(attack, 10, InterferenceMethod.Cut, point).Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_NullOrForeignConnectionDoesNotConsumeOpportunity(bool missing)
    {
        var point = new ConnectionPoint(InterferenceMethod.Cut);
        var attack = Create(point);
        var target = missing ? null : new ConnectionPoint(InterferenceMethod.Cut);
        AssertFailure(attack, 10, InterferenceMethod.Cut, target, CounterFailureReason.ConnectionPointNotFound);
        Assert.True(CounterResolver.Resolve(attack, 10, InterferenceMethod.Cut, point).Success);
    }

    [Fact]
    public void Resolve_OnlyOneSuccessAcrossAllConnectionsAndTicks()
    {
        var first = new ConnectionPoint(InterferenceMethod.Cut);
        var second = new ConnectionPoint(InterferenceMethod.Shoot);
        var attack = Create(first, second);
        Assert.True(CounterResolver.Resolve(attack, 10, InterferenceMethod.Shoot, second).Success);
        AssertFailure(attack, 10, InterferenceMethod.Shoot, second, CounterFailureReason.AlreadyUsed);
        AssertFailure(attack, 10, InterferenceMethod.Cut, first, CounterFailureReason.AlreadyUsed);
        AssertFailure(attack, 20, InterferenceMethod.Cut, first, CounterFailureReason.AlreadyUsed);
        Assert.True(CounterResolver.Resolve(Create(first), 10, InterferenceMethod.Cut, first).Success);
    }

    [Theory]
    [InlineData(InterferenceMethod.Cut)]
    [InlineData(InterferenceMethod.Disrupt)]
    public void Resolve_ConnectionCanAllowMultipleMethods(InterferenceMethod method)
    {
        var point = new ConnectionPoint(InterferenceMethod.Cut | InterferenceMethod.Disrupt);
        Assert.True(CounterResolver.Resolve(Create(point), 10, method, point).Success);
    }

    [Theory]
    [InlineData(15L, 14L)]
    [InlineData(21L, 20L)]
    [InlineData(30L, 10L)]
    public void Resolve_RejectsStaleAttemptsAndExecutedAttacks(long current, long attempt)
    {
        var point = new ConnectionPoint(InterferenceMethod.Cut);
        var attack = Create(point);
        Assert.True(attack.Advance(current));
        AssertFailure(attack, attempt, InterferenceMethod.Cut, point, CounterFailureReason.OutsideWindow);
    }

    [Fact]
    public void Resolve_AcceptsCurrentTickOfAlreadyOpenWindow()
    {
        var point = new ConnectionPoint(InterferenceMethod.Cut);
        var attack = Create(point);
        Assert.True(attack.Advance(15));
        Assert.True(CounterResolver.Resolve(attack, 15, InterferenceMethod.Cut, point).Success);
    }

    [Fact]
    public void Resolve_FailurePrecedenceIsStableAndDirectBreakConsumesWindow()
    {
        var attack = Create(new ConnectionPoint(InterferenceMethod.Cut));
        AssertFailure(attack, 9, (InterferenceMethod)0, null, CounterFailureReason.OutsideWindow);
        AssertFailure(attack, 10, (InterferenceMethod)0, null, CounterFailureReason.ConnectionPointNotFound);
        Assert.True(attack.Advance(10));
        Assert.True(attack.Break());
        AssertFailure(attack, 30, (InterferenceMethod)0, null, CounterFailureReason.AlreadyUsed);
        var result = CounterResolver.Resolve(null, 10, InterferenceMethod.Cut, null);
        Assert.False(result.Success);
        Assert.Equal(CounterFailureReason.OutsideWindow, result.FailureReason);
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(long.MaxValue - 1)]
    public void Resolve_SingleTickWindowWorksAtSignedBoundaries(long tick)
    {
        var point = new ConnectionPoint(InterferenceMethod.Cut);
        Assert.True(TickWindow.TryCreate(tick, tick, out var window));
        Assert.True(JointAttack.TryCreate(tick, window, tick + 1, new[] { point }, out var attack));
        Assert.True(CounterResolver.Resolve(attack, tick, InterferenceMethod.Cut, point).Success);
    }

    [Fact]
    public void TryCreate_RejectsMissingOrInvalidConnections()
    {
        Assert.True(TickWindow.TryCreate(10, 20, out var window));
        ConnectionPoint[]?[] invalid =
        {
            null, Array.Empty<ConnectionPoint>(), new ConnectionPoint[] { null! },
            new[] { new ConnectionPoint((InterferenceMethod)0) },
            new[] { new ConnectionPoint((InterferenceMethod)8) },
            new[] { new ConnectionPoint(InterferenceMethod.Cut), new ConnectionPoint((InterferenceMethod)9) }
        };
        foreach (var points in invalid)
        {
            Assert.False(JointAttack.TryCreate(0, window, 30, points, out var attack));
            Assert.Null(attack);
        }
    }

    [Fact]
    public void TryCreate_SnapshotsConnectionsAndExposesReadOnlyCollection()
    {
        var original = new ConnectionPoint(InterferenceMethod.Cut);
        var replacement = new ConnectionPoint(InterferenceMethod.Shoot);
        var points = new[] { original };
        var attack = Create(points);
        points[0] = replacement;
        Assert.Same(original, Assert.Single(attack.ConnectionPoints));
        var list = Assert.IsAssignableFrom<System.Collections.Generic.IList<ConnectionPoint>>(attack.ConnectionPoints);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list[0] = replacement);
        AssertFailure(attack, 10, InterferenceMethod.Shoot, replacement, CounterFailureReason.ConnectionPointNotFound);
        Assert.True(CounterResolver.Resolve(attack, 10, InterferenceMethod.Cut, original).Success);
    }

    private static void AssertFailure(JointAttack attack, long tick, InterferenceMethod method,
        ConnectionPoint? target, CounterFailureReason reason)
    {
        var previousTick = attack.CurrentTick;
        var previousState = attack.State;
        var result = CounterResolver.Resolve(attack, tick, method, target);
        Assert.False(result.Success);
        Assert.Equal(reason, result.FailureReason);
        Assert.Equal(previousTick, attack.CurrentTick);
        Assert.Equal(previousState, attack.State);
    }

    private static JointAttack Create(params ConnectionPoint[] points)
    {
        Assert.True(TickWindow.TryCreate(10, 20, out var window));
        Assert.True(JointAttack.TryCreate(0, window, 30, points, out var attack));
        Assert.NotNull(attack);
        return attack;
    }
}
