using BlazorDashboard.Charts;

namespace BlazorDashboard.Tests.Charts;

public class NiceScaleTests
{
    [Theory]
    [InlineData("0", "0.05", "0", "0.06", "0.02")]
    [InlineData("-0.025", "0.02", "-0.04", "0.02", "0.02")]
    [InlineData("-0.01", "0.01", "-0.01", "0.01", "0.005")]
    [InlineData("-0.02", "0.02", "-0.02", "0.02", "0.01")]
    [InlineData("0.123", "0.987", "0", "1", "0.25")]
    [InlineData("-3", "7", "-5", "7.5", "2.5")]
    public void For_RoundsRangeOutToStepMultiples(string min, string max, string niceMin, string niceMax, string step)
    {
        var scale = NiceScale.For(D(min), D(max));

        Assert.Equal(new NiceScale(D(niceMin), D(niceMax), D(step)), scale);
    }

    [Fact]
    public void For_FlatAtZero_UsesPlusMinusOnePercent()
    {
        Assert.Equal(new NiceScale(-0.01m, 0.01m, 0.005m), NiceScale.For(0m, 0m));
    }

    [Fact]
    public void For_FlatAwayFromZero_PadsByTenPercent()
    {
        Assert.Equal(new NiceScale(0.045m, 0.055m, 0.0025m), NiceScale.For(0.05m, 0.05m));
    }

    [Theory]
    [InlineData("-0.0137", "0.0421")]
    [InlineData("0.00001", "0.00009")]
    [InlineData("-150", "-149.5")]
    [InlineData("0", "1000000")]
    public void For_AlwaysContainsInputRange(string min, string max)
    {
        var scale = NiceScale.For(D(min), D(max));

        Assert.True(scale.Min <= D(min), $"{scale.Min} > {min}");
        Assert.True(scale.Max >= D(max), $"{scale.Max} < {max}");
        Assert.True(scale.Step > 0);
        Assert.InRange(scale.Ticks.Count(), 2, 11);
    }

    [Fact]
    public void For_RejectsInvertedRange()
    {
        Assert.Throws<ArgumentException>(() => NiceScale.For(1m, 0m));
    }

    [Fact]
    public void Ticks_RunFromMinToMaxInclusive()
    {
        var ticks = new NiceScale(-0.02m, 0.02m, 0.01m).Ticks;

        Assert.Equal([-0.02m, -0.01m, 0m, 0.01m, 0.02m], ticks);
    }

    private static decimal D(string s) => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
}
