using Xunit;

namespace BehindTheCurtain.Core.Tests;

public sealed class TickWindowTests
{
    [Theory]
    [InlineData(10L, 20L)]
    [InlineData(0L, 0L)]
    [InlineData(-20L, -10L)]
    [InlineData(long.MinValue, long.MaxValue)]
    public void TryCreate_PreservesValidBounds(long start, long end)
    {
        Assert.True(TickWindow.TryCreate(start, end, out var window));
        Assert.Equal(start, window.Start);
        Assert.Equal(end, window.End);
    }

    [Theory]
    [InlineData(1L, 0L)]
    [InlineData(-1L, -2L)]
    [InlineData(long.MaxValue, long.MinValue)]
    public void TryCreate_RejectsReversedBounds(long start, long end)
    {
        Assert.False(TickWindow.TryCreate(start, end, out var window));
        Assert.Equal(default(TickWindow), window);
    }

    [Theory]
    [InlineData(9L, false)]
    [InlineData(10L, true)]
    [InlineData(15L, true)]
    [InlineData(20L, true)]
    [InlineData(21L, false)]
    public void Contains_IncludesBothEndpoints(long tick, bool expected)
    {
        var window = Create(10, 20);
        Assert.Equal(expected, window.Contains(tick));
    }

    [Theory]
    [InlineData(0L, 9L, false)]
    [InlineData(0L, 10L, true)]
    [InlineData(5L, 15L, true)]
    [InlineData(10L, 20L, true)]
    [InlineData(12L, 18L, true)]
    [InlineData(0L, 30L, true)]
    [InlineData(15L, 25L, true)]
    [InlineData(20L, 30L, true)]
    [InlineData(21L, 30L, false)]
    [InlineData(10L, 10L, true)]
    [InlineData(20L, 20L, true)]
    public void Overlaps_IsInclusiveAndSymmetric(long start, long end, bool expected)
    {
        var window = Create(10, 20);
        var other = Create(start, end);
        Assert.Equal(expected, window.Overlaps(other));
        Assert.Equal(expected, other.Overlaps(window));
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(0L)]
    [InlineData(long.MaxValue)]
    public void SingleTickWindow_ContainsOnlyItsTick(long tick)
    {
        var window = Create(tick, tick);
        Assert.True(window.Contains(tick));
        Assert.True(window.Overlaps(window));
        if (tick > long.MinValue)
            Assert.False(window.Contains(tick - 1));
        if (tick < long.MaxValue)
            Assert.False(window.Contains(tick + 1));
    }

    [Fact]
    public void ExtremeBounds_DoNotOverflow()
    {
        var full = Create(long.MinValue, long.MaxValue);
        var first = Create(long.MinValue, long.MinValue);
        var last = Create(long.MaxValue, long.MaxValue);
        Assert.True(full.Contains(long.MinValue));
        Assert.True(full.Contains(0));
        Assert.True(full.Contains(long.MaxValue));
        Assert.True(full.Overlaps(first));
        Assert.True(first.Overlaps(full));
        Assert.True(full.Overlaps(last));
        Assert.True(last.Overlaps(full));
        Assert.False(first.Overlaps(last));
        Assert.False(last.Overlaps(first));
    }

    [Fact]
    public void DefaultWindow_IsSingleTickAtZero()
    {
        var window = default(TickWindow);
        Assert.Equal(0L, window.Start);
        Assert.Equal(0L, window.End);
        Assert.True(window.Contains(0));
        Assert.False(window.Contains(-1));
        Assert.False(window.Contains(1));
        Assert.True(window.Overlaps(Create(0, 0)));
    }

    private static TickWindow Create(long start, long end)
    {
        Assert.True(TickWindow.TryCreate(start, end, out var window));
        return window;
    }
}
