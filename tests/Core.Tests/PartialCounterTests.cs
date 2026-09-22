using System.Linq;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class PartialCounterTests
{
    [Theory]
    [InlineData(0, JointAttackState.Executed, 1.0)]
    [InlineData(1, JointAttackState.Weakened, 0.5)]
    [InlineData(2, JointAttackState.Broken, 0.0)]
    public void TwoConnections_ResolveAllOutcomes(int cuts, JointAttackState expected, double severity)
    {
        var attack = Create(2);
        for (var i = 0; i < cuts; i++)
            Assert.True(CounterResolver.Resolve(attack, 10 + i, InterferenceMethod.Cut,
                attack.ConnectionPoints[i]).Success);

        if (cuts < 2)
        {
            Assert.True(attack.Advance(21));
            Assert.Equal(JointAttackState.CounterWindowOpen, attack.State);
            Assert.False(attack.Break(attack.ConnectionPoints[1]));
            Assert.True(attack.Advance(30));
        }
        Assert.Equal(expected, attack.State);
        Assert.Equal(severity, attack.Severity);
        var tick = attack.CurrentTick;
        Assert.False(attack.Advance(31));
        Assert.False(attack.Break());
        Assert.False(attack.Break(attack.ConnectionPoints[1]));
        Assert.False(CounterResolver.Resolve(attack, 31, InterferenceMethod.Cut,
            attack.ConnectionPoints[1]).Success);
        Assert.Equal(tick, attack.CurrentTick);
        Assert.Equal(expected, attack.State);
        Assert.Equal(severity, attack.Severity);
    }

    [Theory]
    [InlineData(9L, false)]
    [InlineData(10L, true)]
    [InlineData(20L, true)]
    [InlineData(21L, false)]
    public void PartialCounter_RespectsInclusiveWindow(long tick, bool success)
    {
        var attack = Create(2);
        Assert.Equal(success, CounterResolver.Resolve(attack, tick, InterferenceMethod.Cut,
            attack.ConnectionPoints[0]).Success);
        Assert.True(attack.Advance(30));
        Assert.Equal(success ? JointAttackState.Weakened : JointAttackState.Executed, attack.State);
        Assert.Equal(success ? 0.5 : 1.0, attack.Severity);
    }

    [Theory]
    [InlineData(10L, 20L)]
    [InlineData(20L, 20L)]
    public void BothConnections_CanBeSeveredAtWindowBoundaries(long first, long second)
    {
        var attack = Create(2);
        Assert.True(CounterResolver.Resolve(attack, first, InterferenceMethod.Cut, attack.ConnectionPoints[0]).Success);
        Assert.True(CounterResolver.Resolve(attack, second, InterferenceMethod.Cut, attack.ConnectionPoints[1]).Success);
        Assert.Equal(JointAttackState.Broken, attack.State);
        Assert.Equal(0.0, attack.Severity);
    }

    [Fact]
    public void DuplicateCounter_DoesNotConsumeOtherConnectionOrChangeSeverity()
    {
        var attack = Create(2);
        Assert.True(CounterResolver.Resolve(attack, 10, InterferenceMethod.Cut, attack.ConnectionPoints[0]).Success);
        var duplicate = CounterResolver.Resolve(attack, 20, InterferenceMethod.Cut, attack.ConnectionPoints[0]);
        Assert.Equal(CounterFailureReason.AlreadyUsed, duplicate.FailureReason);
        Assert.Equal(10L, attack.CurrentTick);
        Assert.Equal(0.5, attack.Severity);
        Assert.Equal(JointAttackState.CounterWindowOpen, attack.State);
        Assert.True(CounterResolver.Resolve(attack, 20, InterferenceMethod.Cut, attack.ConnectionPoints[1]).Success);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    public void Severity_IsRemainingFractionAndAlwaysInRange(int count)
    {
        for (var cuts = 0; cuts <= count; cuts++)
        {
            var attack = Create(count);
            Assert.Equal(1.0, attack.Severity);
            for (var i = 0; i < cuts; i++)
            {
                Assert.True(CounterResolver.Resolve(attack, 10, InterferenceMethod.Cut, attack.ConnectionPoints[i]).Success);
                Assert.InRange(attack.Severity, 0.0, 1.0);
            }
            attack.Advance(30);
            Assert.Equal((double)(count - cuts) / count, attack.Severity);
            Assert.InRange(attack.Severity, 0.0, 1.0);
        }
    }

    [Fact]
    public void DirectBreak_RequiresTargetForMultipleConnections()
    {
        var attack = Create(2);
        Assert.True(attack.Advance(10));
        Assert.False(attack.Break());
        Assert.False(attack.Break(null));
        Assert.False(attack.Break(new ConnectionPoint(InterferenceMethod.Cut)));
        Assert.Equal(1.0, attack.Severity);
        Assert.True(attack.Break(attack.ConnectionPoints[0]));
        Assert.False(attack.Break(attack.ConnectionPoints[0]));
        Assert.Equal(0.5, attack.Severity);
        Assert.True(attack.Break(attack.ConnectionPoints[1]));
        Assert.Equal(JointAttackState.Broken, attack.State);
    }

    [Fact]
    public void TryCreate_RejectsRepeatedConnectionInstance()
    {
        var point = new ConnectionPoint(InterferenceMethod.Cut);
        Assert.True(TickWindow.TryCreate(10, 20, out var window));
        Assert.False(JointAttack.TryCreate(0, window, 30, new[] { point, point }, out var attack));
        Assert.Null(attack);
    }

    private static JointAttack Create(int count)
    {
        var points = Enumerable.Range(0, count).Select(_ => new ConnectionPoint(InterferenceMethod.Cut)).ToArray();
        Assert.True(TickWindow.TryCreate(10, 20, out var window));
        Assert.True(JointAttack.TryCreate(0, window, 30, points, out var attack));
        return attack!;
    }
}
