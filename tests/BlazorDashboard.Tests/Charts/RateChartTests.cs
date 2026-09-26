using System.Globalization;
using BlazorDashboard.Charts;
using BlazorDashboard.Services;
using Bunit;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorDashboard.Tests.Charts;

public class RateChartTests : BunitContext
{
    private static Rate R(int day, string quote, decimal value) => new(new DateOnly(2026, 9, day), "USD", quote, value);

    // Same fixture as RateChartLayoutTests: EUR 0 → +1% → +2%, GBP 0 → -1% → -2% on Sep 1, 2, 4.
    private static readonly Rate[] TwoSeries =
    [
        R(1, "EUR", 1.00m), R(2, "EUR", 1.01m), R(4, "EUR", 1.02m),
        R(1, "GBP", 0.50m), R(2, "GBP", 0.495m), R(4, "GBP", 0.49m),
    ];

    public RateChartTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
    }

    private IRenderedComponent<RateChart> RenderChart(
        IReadOnlyList<Rate>? rows = null, string[]? quotes = null, IReadOnlyDictionary<string, int>? slots = null) =>
        Render<RateChart>(p => p
            .Add(c => c.Rows, rows ?? TwoSeries)
            .Add(c => c.Quotes, quotes ?? ["EUR", "GBP"])
            .Add(c => c.BaseCode, "USD")
            .Add(c => c.ColorSlots, slots));

    private static string[] Texts(IRenderedComponent<RateChart> cut, string selector) =>
        cut.FindAll(selector).Select(e => e.TextContent.Trim()).ToArray();

    // --- Static render -------------------------------------------------------

    [Fact]
    public void NoRows_RendersNothing()
    {
        var cut = RenderChart(rows: []);

        Assert.Equal("", cut.Markup.Trim());
    }

    [Fact]
    public void Svg_IsAnAccessibleFocusableImage()
    {
        var svg = RenderChart().Find("svg.chart-svg");

        // 92 units right of the plot leave room for direct labels like "XAU -10.53%".
        Assert.Equal("0 0 820 320", svg.GetAttribute("viewBox"));
        Assert.Equal("img", svg.GetAttribute("role"));
        Assert.Equal("0", svg.GetAttribute("tabindex"));
        Assert.Equal(
            "Line chart of percent change against USD for EUR, GBP from Sep 1, 2026 to Sep 4, 2026. Use the arrow keys to read values by date.",
            svg.GetAttribute("aria-label"));
    }

    [Fact]
    public void Title_NamesTheMeasureAndStartDate()
    {
        var cut = RenderChart();

        Assert.Equal("Change since Sep 1", cut.Find(".chart-title").TextContent.Trim());
        Assert.Contains("% change in the price of 1 USD", cut.Find(".chart-subtitle").TextContent);
    }

    [Fact]
    public void DrawsOneLinePerSeries_ColoredByTheirSlot()
    {
        var cut = RenderChart(slots: new Dictionary<string, int> { ["EUR"] = 3, ["GBP"] = 1 });

        var lines = cut.FindAll("polyline.series-line");
        Assert.Equal(2, lines.Count);
        Assert.Equal("56,152 280,84 728,16", lines[0].GetAttribute("points"));
        Assert.Contains("slot-3", lines[0].ClassList);
        Assert.Contains("slot-1", lines[1].ClassList);

        var ends = cut.FindAll("circle.series-end");
        Assert.Equal(["728", "16"], [ends[0].GetAttribute("cx")!, ends[0].GetAttribute("cy")!]);
        Assert.Contains("slot-3", ends[0].ClassList);
    }

    [Fact]
    public void WithoutColorSlots_UsesSeriesOrder()
    {
        var cut = RenderChart();

        var lines = cut.FindAll("polyline.series-line");
        Assert.Contains("slot-1", lines[0].ClassList);
        Assert.Contains("slot-2", lines[1].ClassList);
    }

    [Fact]
    public void DrawsGridlinesWithAStrongerZeroLine()
    {
        var cut = RenderChart();

        Assert.Equal(5, cut.FindAll("line.gridline").Count);
        var zero = cut.Find("line.gridline.zero-line");
        Assert.Equal("152", zero.GetAttribute("y1"));
        Assert.Equal("56", zero.GetAttribute("x1"));
        Assert.Equal("728", zero.GetAttribute("x2"));
    }

    [Fact]
    public void LabelsBothAxes()
    {
        var cut = RenderChart();

        Assert.Equal(["-2%", "-1%", "0%", "+1%", "+2%"], Texts(cut, "text.tick-label.y"));
        Assert.Equal(["Sep 1", "Sep 2", "Sep 4"], Texts(cut, "text.tick-label.x"));
    }

    [Fact]
    public void Legend_ListsSeriesWhenThereAreTwoOrMore()
    {
        var cut = RenderChart(slots: new Dictionary<string, int> { ["EUR"] = 3, ["GBP"] = 1 });

        Assert.Equal(["EUR", "GBP"], Texts(cut, ".chart-legend li"));
        Assert.Contains("slot-3", cut.Find(".chart-legend li .legend-key").ClassList);
    }

    [Fact]
    public void Legend_IsOmittedForASingleSeries()
    {
        var cut = RenderChart(rows: TwoSeries.Where(r => r.Quote == "EUR").ToList(), quotes: ["EUR"]);

        Assert.Single(cut.FindAll("polyline.series-line"));
        Assert.Empty(cut.FindAll(".chart-legend"));
    }

    [Fact]
    public void DirectLabels_ShowQuoteAndLatestChangeAtLineEnds()
    {
        var cut = RenderChart();

        var labels = cut.FindAll("text.direct-label");
        Assert.Equal(["EUR +2.00%", "GBP -2.00%"], labels.Select(l => l.TextContent.Trim()));
        Assert.Equal("16", labels[0].GetAttribute("y"));
        Assert.Equal("288", labels[1].GetAttribute("y"));
        Assert.Equal("736", labels[0].GetAttribute("x"));
    }

    [Fact]
    public void DirectLabels_AreDroppedAboveFourSeries()
    {
        string[] quotes = ["A", "B", "C", "D", "E"];
        var rows = quotes.SelectMany(q => new[] { R(1, q, 1m), R(2, q, 1.01m) }).ToList();

        var cut = RenderChart(rows: rows, quotes: quotes);

        Assert.Empty(cut.FindAll("text.direct-label"));
        Assert.Equal(5, cut.FindAll(".chart-legend li").Count);
    }

    [Fact]
    public void HasOneHitBandPerDate()
    {
        var bands = RenderChart().FindAll("rect.hit");

        Assert.Equal(3, bands.Count);
        Assert.Equal("168", bands[1].GetAttribute("x"));
        Assert.Equal("336", bands[1].GetAttribute("width"));
        Assert.Equal("16", bands[1].GetAttribute("y"));
        Assert.Equal("272", bands[1].GetAttribute("height"));
    }

    // --- Hover ----------------------------------------------------------------

    [Fact]
    public void NoTooltipOrCrosshairUntilHovered()
    {
        var cut = RenderChart();

        Assert.Equal(3, cut.FindAll("rect.hit").Count);
        Assert.Empty(cut.FindAll(".chart-tooltip"));
        Assert.Empty(cut.FindAll("line.crosshair"));
    }

    [Fact]
    public void HoveringABand_ShowsCrosshairAndTooltipForEverySeries()
    {
        var cut = RenderChart();

        cut.FindAll("rect.hit")[1].MouseEnter();

        var crosshair = cut.Find("line.crosshair");
        Assert.Equal("280", crosshair.GetAttribute("x1"));
        Assert.Equal("280", crosshair.GetAttribute("x2"));
        Assert.Equal(2, cut.FindAll("circle.hover-dot").Count);

        var tooltip = cut.Find(".chart-tooltip");
        Assert.Equal("Sep 2, 2026", tooltip.QuerySelector(".tooltip-date")!.TextContent.Trim());
        Assert.Equal(
            ["+1.00% EUR 1.0100", "-1.00% GBP 0.495"],
            tooltip.QuerySelectorAll(".tooltip-row").Select(r => string.Join(' ', r.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))));
        Assert.Contains("left: 34.15%", tooltip.GetAttribute("style")); // 280 / 820
    }

    [Fact]
    public void Tooltip_ShowsDashForSeriesWithoutDataOnThatDate()
    {
        var rows = TwoSeries.Append(R(3, "EUR", 1.015m)).ToList();
        var cut = RenderChart(rows: rows);

        cut.FindAll("rect.hit")[2].MouseEnter(); // Sep 3: EUR only

        var rowTexts = cut.FindAll(".tooltip-row").Select(r => r.TextContent).ToList();
        Assert.Contains("+1.50%", rowTexts[0]);
        Assert.Contains("—", rowTexts[1]);
        Assert.Single(cut.FindAll("circle.hover-dot"));
    }

    [Fact]
    public void LeavingTheChart_HidesTheTooltip()
    {
        var cut = RenderChart();
        cut.FindAll("rect.hit")[0].MouseEnter();

        cut.Find("svg.chart-svg").MouseLeave();

        Assert.Empty(cut.FindAll(".chart-tooltip"));
    }

    // --- Keyboard ---------------------------------------------------------------

    private static void Press(IRenderedComponent<RateChart> cut, string key) =>
        cut.Find("svg.chart-svg").KeyDown(new KeyboardEventArgs { Key = key });

    private static string? TooltipDate(IRenderedComponent<RateChart> cut) =>
        cut.FindAll(".tooltip-date").SingleOrDefault()?.TextContent.Trim();

    [Fact]
    public void ArrowKeys_StepThroughDates()
    {
        var cut = RenderChart();

        Press(cut, "ArrowRight");
        Assert.Equal("Sep 1, 2026", TooltipDate(cut));
        Press(cut, "ArrowRight");
        Assert.Equal("Sep 2, 2026", TooltipDate(cut));
        Press(cut, "ArrowLeft");
        Assert.Equal("Sep 1, 2026", TooltipDate(cut));
        Press(cut, "ArrowLeft");
        Assert.Equal("Sep 1, 2026", TooltipDate(cut));
    }

    [Fact]
    public void ArrowLeftFirst_StartsAtTheLatestDate()
    {
        var cut = RenderChart();

        Press(cut, "ArrowLeft");

        Assert.Equal("Sep 4, 2026", TooltipDate(cut));
        Press(cut, "ArrowRight");
        Assert.Equal("Sep 4, 2026", TooltipDate(cut));
    }

    [Fact]
    public void HomeEndAndEscape()
    {
        var cut = RenderChart();

        Press(cut, "End");
        Assert.Equal("Sep 4, 2026", TooltipDate(cut));
        Press(cut, "Home");
        Assert.Equal("Sep 1, 2026", TooltipDate(cut));
        Press(cut, "Escape");
        Assert.Null(TooltipDate(cut));
        Press(cut, "a");
        Assert.Null(TooltipDate(cut));
    }

    [Fact]
    public void NewRows_ClearTheHover()
    {
        var cut = RenderChart();
        Press(cut, "End");

        cut.Render(p => p.Add(c => c.Rows, TwoSeries.Take(2).ToList()));

        Assert.Null(TooltipDate(cut));
    }

    // --- Table view ---------------------------------------------------------------

    [Fact]
    public void DataTable_ListsRatesByDate()
    {
        var rows = TwoSeries.Append(R(3, "EUR", 1.015m)).ToList();
        var cut = RenderChart(rows: rows);

        var table = cut.Find("details.chart-table table");
        Assert.Equal("Show data table", cut.Find("details.chart-table summary").TextContent.Trim());
        Assert.Equal(["Date", "EUR", "GBP"], table.QuerySelectorAll("thead th").Select(th => th.TextContent.Trim()));
        var body = table.QuerySelectorAll("tbody tr")
            .Select(tr => tr.QuerySelectorAll("th, td").Select(c => c.TextContent.Trim()).ToArray())
            .ToList();
        Assert.Equal(
            [
                ["Sep 1, 2026", "1.0000", "0.5"],
                ["Sep 2, 2026", "1.0100", "0.495"],
                ["Sep 3, 2026", "1.0150", "—"],
                ["Sep 4, 2026", "1.0200", "0.49"],
            ],
            body);
    }
}
