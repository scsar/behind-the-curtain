using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class JointAttackTests
{
    [Theory]
    [InlineData(0L, 10L, 20L, 30L)]
    [InlineData(0L, 0L, 0L, 1L)]
    [InlineData(-30L, -20L, -10L, -1L)]
    [InlineData(long.MinValue, long.MinValue, long.MinValue, long.MaxValue)]
    public void TryCreate_PreservesScheduleAndStartsTelegraphing(long start, long open, long close, long execution)
    {
        var attack = Create(start, open, close, execution);
        Assert.Equal(start, attack.TelegraphStartTick);
        Assert.Equal(start, attack.CurrentTick);
        Assert.Equal(open, attack.CounterWindow.Start);
        Assert.Equal(close, attack.CounterWindow.End);
        Assert.Equal(execution, attack.ExecutionTick);
        Assert.Equal(JointAttackState.Telegraphing, attack.State);
        Assert.False(attack.Break());
    }

    [Theory]
    [InlineData(11L, 10L, 20L, 30L)]
    [InlineData(0L, 10L, 20L, 20L)]
    [InlineData(0L, 10L, 20L, 19L)]
    [InlineData(0L, 10L, 20L, 5L)]
    [InlineData(0L, 10L, long.MaxValue, long.MaxValue)]
    public void TryCreate_RejectsInvalidSchedule(long start, long open, long close, long execution)
    {
        Assert.True(TickWindow.TryCreate(open, close, out var window));
        Assert.False(JointAttack.TryCreate(start, window, execution,
            new[] { new ConnectionPoint(InterferenceMethod.Cut) }, out var attack));
        Assert.Null(attack);
    }

    [Fact]
    public void Advance_FollowsFullExecutionPath()
    {
        var attack = Create();
        foreach (var tick in new long[] { 0, 9 })
        {
            Assert.True(attack.Advance(tick));
            Assert.Equal(JointAttackState.Telegraphing, attack.State);
            Assert.False(attack.Break());
        }
        foreach (var tick in new long[] { 10, 20, 21, 29 })
        {
            Assert.True(attack.Advance(tick));
            Assert.Equal(JointAttackState.CounterWindowOpen, attack.State);
        }
        Assert.False(attack.Break());
        Assert.True(attack.Advance(30));
        Assert.Equal(JointAttackState.Executed, attack.State);
        Assert.Equal(30L, attack.CurrentTick);
    }

    [Theory]
    [InlineData(10L)]
    [InlineData(15L)]
    [InlineData(20L)]
    public void Break_WithinInclusiveWindowTransitionsToBroken(long tick)
    {
        var attack = Create();
        Assert.True(attack.Advance(tick));
        Assert.True(attack.Break());
        Assert.Equal(JointAttackState.Broken, attack.State);
        Assert.Equal(tick, attack.CurrentTick);
    }

    [Theory]
    [InlineData(0L, JointAttackState.Telegraphing)]
    [InlineData(9L, JointAttackState.Telegraphing)]
    [InlineData(21L, JointAttackState.CounterWindowOpen)]
    [InlineData(29L, JointAttackState.CounterWindowOpen)]
    [InlineData(30L, JointAttackState.Executed)]
    public void Break_OutsideWindowRejectsWithoutMutation(long tick, JointAttackState state)
    {
        var attack = Create();
        Assert.True(attack.Advance(tick));
        Assert.False(attack.Break());
        Assert.Equal(state, attack.State);
        Assert.Equal(tick, attack.CurrentTick);
    }

    [Theory]
    [InlineData(0L, -1L)]
    [InlineData(9L, 0L)]
    [InlineData(15L, 9L)]
    [InlineData(21L, 20L)]
    public void Advance_RejectsBackwardsTimeWithoutMutation(long current, long previous)
    {
        var attack = Create();
        Assert.True(attack.Advance(current));
        var state = attack.State;
        Assert.False(attack.Advance(previous));
        Assert.Equal(state, attack.State);
        Assert.Equal(current, attack.CurrentTick);
        Assert.Equal(attack.CounterWindow.Contains(current), attack.Break());
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(10L)]
    [InlineData(21L)]
    public void Advance_SameTickIsAcceptedBeforeTerminalState(long tick)
    {
        var attack = Create();
        Assert.True(attack.Advance(tick));
        var state = attack.State;
        Assert.True(attack.Advance(tick));
        Assert.Equal(state, attack.State);
        Assert.Equal(tick, attack.CurrentTick);
    }

    [Theory]
    [InlineData(false, 30L)]
    [InlineData(false, long.MaxValue)]
    [InlineData(true, 30L)]
    [InlineData(true, long.MaxValue)]
    public void Advance_CanSkipToExecution(bool openFirst, long tick)
    {
        var attack = Create();
        if (openFirst)
            Assert.True(attack.Advance(10));
        Assert.True(attack.Advance(tick));
        Assert.Equal(JointAttackState.Executed, attack.State);
        Assert.Equal(tick, attack.CurrentTick);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TerminalStates_RejectAllFurtherOperations(bool broken)
    {
        var attack = Create();
        Assert.True(attack.Advance(broken ? 10 : 30));
        if (broken)
            Assert.True(attack.Break());
        var state = attack.State;
        var tick = attack.CurrentTick;
        Assert.False(attack.Break());
        foreach (var next in new long[] { long.MinValue, 0, 10, 20, tick, 30, long.MaxValue })
        {
            Assert.False(attack.Advance(next));
            Assert.Equal(state, attack.State);
            Assert.Equal(tick, attack.CurrentTick);
        }
    }

    [Theory]
    [InlineData(long.MinValue, long.MinValue, long.MinValue + 1)]
    [InlineData(-2L, -1L, 0L)]
    [InlineData(0L, 0L, 1L)]
    [InlineData(long.MaxValue - 1, long.MaxValue - 1, long.MaxValue)]
    public void SingleTickWindows_WorkAtSignedTickBoundaries(long start, long windowTick, long execution)
    {
        var broken = Create(start, windowTick, windowTick, execution);
        Assert.True(broken.Advance(windowTick));
        Assert.True(broken.Break());
        Assert.Equal(JointAttackState.Broken, broken.State);

        var executed = Create(start, windowTick, windowTick, execution);
        Assert.True(executed.Advance(windowTick));
        Assert.True(executed.Advance(execution));
        Assert.Equal(JointAttackState.Executed, executed.State);
    }

    private static JointAttack Create(long start = 0, long open = 10, long close = 20, long execution = 30)
    {
        Assert.True(TickWindow.TryCreate(open, close, out var window));
        Assert.True(JointAttack.TryCreate(start, window, execution,
            new[] { new ConnectionPoint(InterferenceMethod.Cut) }, out var attack));
        Assert.NotNull(attack);
        return attack;
    }
}
