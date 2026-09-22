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
        var allLines = result.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Settings Seed=1 Encounters=3 Decay=0.5 AreaThreshold=0.5 HealerThreshold=0.5 RangedThreshold=0.5 Strategies=Alternating(AreaFirst,HealerFirst)", allLines[0].TrimEnd());
        var lines = allLines[1..];
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("Encounter 1 [AreaFirst] Area=0.70", lines[0]);
        Assert.StartsWith("Encounter 2 [HealerFirst] Area=0.00", lines[1]);
        Assert.StartsWith("Encounter 3 [AreaFirst] Area=0.70", lines[2]);
        Assert.Contains("NextTactic=SpreadFormation", lines[0]);
        // Current healer focus alone would choose GuardHealer; expedition memory changes that choice.
        Assert.Contains("NextTactic=StandardFormation", lines[1]);
        Assert.Contains("Memory Area=0.35 Healer=0.48 Ranged=0.40", lines[1]);
        foreach (var line in lines)
        {
            Assert.Matches(@"Area=\d\.\d{2} Healer=\d\.\d{2} Ranged=\d\.\d{2} NextTactic=\w+ Counter=[0-2]/2", line);
            Assert.Matches(@"Memory Area=\d\.\d{2} Healer=\d\.\d{2} Ranged=\d\.\d{2}", line);
        }
    }

    [Theory]
    [InlineData("--seed")]
    [InlineData("--seed", "bad")]
    [InlineData("--seed", "2147483648")]
    [InlineData("--encounters", "0")]
    [InlineData("--encounters", "-1")]
    [InlineData("--unknown", "1")]
    [InlineData("--seed", "1", "--seed", "2")]
    [InlineData("--decay", "1")]
    [InlineData("--decay", "-0.01")]
    [InlineData("--decay", "NaN")]
    [InlineData("--decay", "Infinity")]
    [InlineData("--threshold", "1.01")]
    [InlineData("--threshold", "-0.01")]
    [InlineData("--threshold", "NaN")]
    [InlineData("--area-threshold", "1.01")]
    [InlineData("--healer-threshold", "-0.01")]
    [InlineData("--ranged-threshold", "Infinity")]
    [InlineData("--threshold", "0,5")]
    [InlineData("--threshold")]
    [InlineData("--strategies", "")]
    [InlineData("--strategies", "AreaFirst,")]
    [InlineData("--strategies", "Unknown")]
    [InlineData("--strategies", "0")]
    [InlineData("--strategies", "areafirst")]
    public void InvalidArgumentsAreRejectedWithoutSimulation(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(2, Simulation.Run(args, output, error));
        Assert.Equal("", output.ToString());
        Assert.Contains("Usage:", error.ToString());
        Assert.DoesNotContain("Usage:", error.ToString().Split('\n')[0]);
        Assert.False(string.IsNullOrWhiteSpace(error.ToString().Split('\n')[0]));
    }

    [Fact]
    public void OutputUsesInvariantDecimalSeparator()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            var args = new[] { "--decay", "0.25", "--threshold", "0.6", "--area-threshold", "0.7" };
            var expected = Run(args);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(expected, Run(args));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("--decay", "0", "Decay=0")]
    [InlineData("--decay", "0.999", "Decay=0.999")]
    [InlineData("--threshold", "0", "AreaThreshold=0 HealerThreshold=0 RangedThreshold=0")]
    [InlineData("--threshold", "1", "AreaThreshold=1 HealerThreshold=1 RangedThreshold=1")]
    [InlineData("--area-threshold", "0.2", "AreaThreshold=0.2 HealerThreshold=0.5 RangedThreshold=0.5")]
    [InlineData("--healer-threshold", "0.3", "AreaThreshold=0.5 HealerThreshold=0.3 RangedThreshold=0.5")]
    [InlineData("--ranged-threshold", "0.4", "AreaThreshold=0.5 HealerThreshold=0.5 RangedThreshold=0.4")]
    public void ParsesSettings(string option, string value, string expected)
    {
        Assert.Contains(expected, Run(new[] { option, value }).Split('\n')[0]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IndividualThresholdsOverrideCommonThresholdInEitherOrder(bool commonFirst)
    {
        var individual = new[] { "--area-threshold", "0.2", "--healer-threshold", "0.3", "--ranged-threshold", "0.4" };
        string[] args = commonFirst ? ["--threshold", "0.9", .. individual] : [.. individual, "--threshold", "0.9"];
        Assert.Contains("AreaThreshold=0.2 HealerThreshold=0.3 RangedThreshold=0.4", Run(args).Split('\n')[0]);
    }

    [Theory]
    [InlineData("HealerFirst", "HealerFirst", "HealerFirst", "HealerFirst")]
    [InlineData("HealerFirst,AreaFirst", "HealerFirst", "AreaFirst", "AreaFirst")]
    [InlineData("AreaFirst,HealerFirst,AreaFirst,HealerFirst", "AreaFirst", "HealerFirst", "AreaFirst")]
    public void StrategiesFollowListAndRepeatLast(string list, string first, string second, string third)
    {
        var lines = Run(new[] { "--strategies", list }).Split('\n');
        Assert.Contains("Strategies=" + list, lines[0]);
        Assert.StartsWith($"Encounter 1 [{first}]", lines[1]);
        Assert.StartsWith($"Encounter 2 [{second}]", lines[2]);
        Assert.StartsWith($"Encounter 3 [{third}]", lines[3]);
    }

    [Fact]
    public void SettingsAffectMemoryAndTacticAndRemainDeterministic()
    {
        var args = new[] { "--seed", "1", "--encounters", "4", "--decay", "0", "--threshold", "1",
            "--healer-threshold", "0", "--strategies", "AreaFirst,HealerFirst" };
        var result = Run(args);
        Assert.Equal(result, Run(args));
        var lines = result.Split('\n');
        Assert.Contains("Memory Area=0.00 Healer=0.80 Ranged=0.80", lines[2]);
        for (var i = 1; i <= 4; i++) Assert.Contains("NextTactic=GuardHealer", lines[i]);
        Assert.Contains("NextTactic=StandardFormation", Run(new[] { "--threshold", "1" }));
    }

    [Fact]
    public void LegacyArgumentsPreserveNextTacticSequence()
    {
        var lines = Run(new[] { "--seed", "1", "--encounters", "8" }).Split('\n');
        var expected = new[] { "SpreadFormation", "StandardFormation", "SpreadFormation", "GuardHealer",
            "StandardFormation", "GuardHealer", "StandardFormation", "GuardHealer" };
        for (var i = 0; i < expected.Length; i++)
            Assert.Contains($"NextTactic={expected[i]} ", lines[i + 1]);
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
