using System.Globalization;
using System.IO;
using BehindTheCurtain.Core.Sim;
using Xunit;

namespace BehindTheCurtain.Core.Tests;

public class SimulationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(-42)]
    [InlineData(int.MaxValue)]
    public void SameSeedProducesSameOutput(int seed)
    {
        var args = new[] { "--seed", seed.ToString(CultureInfo.InvariantCulture), "--encounters", "8" };
        var first = Run(args);
        Run(new[] { "--seed", "123" });
        Assert.Equal(first, Run(args));
    }

    [Fact]
    public void DifferentSeedsCanProduceDifferentObservationsAndCounters()
    {
        Assert.NotEqual(Run(new[] { "--seed", "1" }), Run(new[] { "--seed", "2" }));
    }

    [Fact]
    public void DefaultsAlternateStrategiesAndReportObservedRatiosAndNextTactic()
    {
        var result = Run(System.Array.Empty<string>());
        Assert.Equal(result, Run(new[] { "--seed", "1", "--encounters", "3" }));
        var lines = result.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("Encounter 1 [AreaFirst] Area=0.70", lines[0]);
        Assert.StartsWith("Encounter 2 [HealerFirst] Area=0.00", lines[1]);
        Assert.StartsWith("Encounter 3 [AreaFirst] Area=0.70", lines[2]);
        Assert.Contains("NextTactic=SpreadFormation", lines[0]);
        // Current healer focus alone would choose GuardHealer; expedition memory changes that choice.
        Assert.Contains("NextTactic=StandardFormation", lines[1]);
        foreach (var line in lines)
            Assert.Matches(@"Area=\d\.\d{2} Healer=\d\.\d{2} Ranged=\d\.\d{2} NextTactic=\w+ Counter=[0-2]/2", line);
    }

    [Theory]
    [InlineData("--seed")]
    [InlineData("--seed", "bad")]
    [InlineData("--seed", "2147483648")]
    [InlineData("--encounters", "0")]
    [InlineData("--encounters", "-1")]
    [InlineData("--unknown", "1")]
    [InlineData("--seed", "1", "--seed", "2")]
    public void InvalidArgumentsAreRejectedWithoutSimulation(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(1, Simulation.Run(args, output, error));
        Assert.Equal("", output.ToString());
        Assert.Contains("Usage:", error.ToString());
    }

    [Fact]
    public void OutputUsesInvariantDecimalSeparator()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            var expected = Run(System.Array.Empty<string>());
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(expected, Run(System.Array.Empty<string>()));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private static string Run(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, Simulation.Run(args, output, error));
        Assert.Equal("", error.ToString());
        return output.ToString();
    }
}
