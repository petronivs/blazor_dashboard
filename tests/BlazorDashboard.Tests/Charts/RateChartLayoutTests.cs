using System.Globalization;
using BlazorDashboard.Charts;
using BlazorDashboard.Services;

namespace BlazorDashboard.Tests.Charts;

public class RateChartLayoutTests
{
    private static readonly CultureInfo EnUs = new("en-US");

    private static Rate R(int month, int day, string quote, decimal value, int year = 2026) =>
        new(new DateOnly(year, month, day), "USD", quote, value);

    // Sep 1, Sep 2, Sep 4 (Sep 3 missing, like a holiday). EUR rises 0 → +1% → +2%, GBP falls 0 → -1% → -2%.
    // Y scale is exactly -2%..+2% (step 1%); the plot is x 56..728, y 16..288.
    private static readonly Rate[] TwoSeries =
    [
        R(9, 4, "EUR", 1.02m), R(9, 1, "EUR", 1.00m), R(9, 2, "EUR", 1.01m),
        R(9, 1, "GBP", 0.50m), R(9, 2, "GBP", 0.495m), R(9, 4, "GBP", 0.49m),
    ];

    private static RateChartLayout Build(IEnumerable<Rate> rows, params string[] order) =>
        RateChartLayout.Build(rows, order, EnUs);

    [Fact]
    public void EmptyInput_IsEmpty()
    {
        var layout = Build([], "EUR");

        Assert.True(layout.IsEmpty);
        Assert.Empty(layout.Series);
        Assert.Empty(layout.YTicks);
        Assert.Empty(layout.XTicks);
        Assert.Empty(layout.Columns);
    }

    [Fact]
    public void Series_FollowQuoteOrder_WithPointsSortedByDate()
    {
        var layout = Build(TwoSeries, "GBP", "EUR");

        Assert.False(layout.IsEmpty);
        Assert.Equal(["GBP", "EUR"], layout.Series.Select(s => s.Quote));
        Assert.Equal(
            [new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 4)],
            layout.Series[1].Points.Select(p => p.Date));
    }

    [Fact]
    public void Series_NotInQuoteOrder_ComeFirst()
    {
        var layout = Build(TwoSeries, "EUR");

        Assert.Equal(["GBP", "EUR"], layout.Series.Select(s => s.Quote));
    }

    [Fact]
    public void Points_CarryRateAndChangeSinceFirstPoint()
    {
        var eur = Build(TwoSeries, "EUR", "GBP").Series[0];

        Assert.Equal([1.00m, 1.01m, 1.02m], eur.Points.Select(p => p.Rate));
        Assert.Equal([0m, 0.01m, 0.02m], eur.Points.Select(p => p.Change));
        Assert.Same(eur.Points[2], eur.Last);
    }

    [Fact]
    public void Points_AreScaledByCalendarDateAndChange()
    {
        var layout = Build(TwoSeries, "EUR", "GBP");

        // x: Sep 1 → 56, Sep 4 → 728, Sep 2 is one third of the way. y: +2% → 16, 0 → 152, -2% → 288.
        Assert.Equal([(56d, 152d), (280d, 84d), (728d, 16d)], layout.Series[0].Points.Select(p => (p.X, p.Y)));
        Assert.Equal([(56d, 152d), (280d, 220d), (728d, 288d)], layout.Series[1].Points.Select(p => (p.X, p.Y)));
        Assert.Equal(152d, layout.ZeroY);
    }

    [Fact]
    public void SvgPoints_IsSpaceSeparatedXyPairs()
    {
        var eur = Build(TwoSeries, "EUR", "GBP").Series[0];

        Assert.Equal("56,152 280,84 728,16", eur.SvgPoints);
    }

    [Fact]
    public void SvgPoints_UsesInvariantDecimalsRoundedToTwoPlaces()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var series = new ChartSeries("EUR", [new ChartPoint(new DateOnly(2026, 9, 1), 1m, 0m, 56.123, 151.999)], 0);

            Assert.Equal("56.12,152", series.SvgPoints);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void YTicks_AreNicePercentagesPositionedOnThePlot()
    {
        var layout = Build(TwoSeries, "EUR", "GBP");

        Assert.Equal(
            [new AxisTick(288, "-2%"), new AxisTick(220, "-1%"), new AxisTick(152, "0%"), new AxisTick(84, "+1%"), new AxisTick(16, "+2%")],
            layout.YTicks);
    }

    [Fact]
    public void YTicks_ShowFractionalPercentages()
    {
        // 0 → +0.4%: step 0.1%, so labels need a decimal place.
        var layout = Build([R(9, 1, "EUR", 1m), R(9, 2, "EUR", 1.004m)], "EUR");

        Assert.Equal(["0%", "+0.1%", "+0.2%", "+0.3%", "+0.4%"], layout.YTicks.Select(t => t.Label));
    }

    [Fact]
    public void XTicks_UseActualDatesWithShortLabels()
    {
        var layout = Build(TwoSeries, "EUR", "GBP");

        Assert.Equal([new AxisTick(56, "Sep 1"), new AxisTick(280, "Sep 2"), new AxisTick(728, "Sep 4")], layout.XTicks);
    }

    [Fact]
    public void XTicks_AreLimitedToFiveEvenlySpacedDates()
    {
        var rows = Enumerable.Range(1, 30).Select(d => R(9, d, "EUR", 1m + d / 1000m));

        var layout = Build(rows, "EUR");

        Assert.Equal(["Sep 1", "Sep 8", "Sep 16", "Sep 23", "Sep 30"], layout.XTicks.Select(t => t.Label));
    }

    [Fact]
    public void XTicks_IncludeYearForRangesOverAYear()
    {
        var layout = Build([R(1, 2, "EUR", 1m, 2025), R(3, 2, "EUR", 1.1m)], "EUR");

        Assert.Equal(["Jan 2025", "Mar 2026"], layout.XTicks.Select(t => t.Label));
    }

    [Fact]
    public void Columns_SplitThePlotAtMidpointsBetweenDates()
    {
        var layout = Build(TwoSeries, "EUR", "GBP");

        Assert.Equal(
            [
                new ChartColumn(new DateOnly(2026, 9, 1), 56, 56, 168),
                new ChartColumn(new DateOnly(2026, 9, 2), 280, 168, 504),
                new ChartColumn(new DateOnly(2026, 9, 4), 728, 504, 728),
            ],
            layout.Columns);
    }

    [Fact]
    public void SingleDate_IsCenteredAndSpansThePlot()
    {
        var layout = Build([R(9, 1, "EUR", 0.9m)], "EUR");

        var point = Assert.Single(layout.Series[0].Points);
        Assert.Equal(392d, point.X);
        Assert.Equal(0m, point.Change);
        Assert.Equal(new ChartColumn(new DateOnly(2026, 9, 1), 392, 56, 728), Assert.Single(layout.Columns));
        Assert.Equal([new AxisTick(392, "Sep 1")], layout.XTicks);
    }

    [Fact]
    public void Series_WithMissingDates_OnlyPlotTheirOwnPoints()
    {
        var rows = TwoSeries.Append(R(9, 3, "EUR", 1.015m));

        var layout = Build(rows, "EUR", "GBP");

        Assert.Equal(4, layout.Series[0].Points.Count);
        Assert.Equal(3, layout.Series[1].Points.Count);
        Assert.Equal(4, layout.Columns.Count);
    }

    [Fact]
    public void ZeroFirstRate_PlotsAsNoChange()
    {
        var layout = Build([R(9, 1, "EUR", 0m), R(9, 2, "EUR", 1m)], "EUR");

        Assert.All(layout.Series[0].Points, p => Assert.Equal(0m, p.Change));
    }

    [Fact]
    public void LabelY_StartsAtTheLastPoint()
    {
        var layout = Build(TwoSeries, "EUR", "GBP");

        Assert.Equal(16d, layout.Series[0].LabelY);
        Assert.Equal(288d, layout.Series[1].LabelY);
    }

    [Fact]
    public void LabelY_SpreadsOverlappingLabelsApart()
    {
        // All three end at +1%; labels must sit LabelGap apart, keep series order top-down, and stay in the plot.
        var rows = new[] { "EUR", "GBP", "CHF" }.SelectMany(q => new[] { R(9, 1, q, 1m), R(9, 2, q, 1.01m) })
            .Append(R(9, 1, "JPY", 1m)).Append(R(9, 2, "JPY", 0.99m));

        var layout = Build(rows, "EUR", "GBP", "CHF", "JPY");

        var ys = layout.Series.Take(3).Select(s => s.LabelY).ToArray();
        Assert.Equal(RateChartLayout.LabelGap, ys[1] - ys[0], 3);
        Assert.Equal(RateChartLayout.LabelGap, ys[2] - ys[1], 3);
        Assert.All(layout.Series, s => Assert.InRange(s.LabelY, RateChartLayout.PlotTop, RateChartLayout.PlotBottom));
    }

    [Fact]
    public void LabelY_PushedPastTheBottom_ShiftBackUp()
    {
        // Three series all end at the bottom of the plot.
        var rows = new[] { "EUR", "GBP", "CHF" }.SelectMany(q => new[] { R(9, 1, q, 1m), R(9, 2, q, 0.99m) });

        var layout = Build(rows, "EUR", "GBP", "CHF");

        Assert.Equal(
            [RateChartLayout.PlotBottom - 2 * RateChartLayout.LabelGap, RateChartLayout.PlotBottom - RateChartLayout.LabelGap, RateChartLayout.PlotBottom],
            layout.Series.Select(s => s.LabelY));
    }
}
