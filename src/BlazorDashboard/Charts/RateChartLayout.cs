using System.Globalization;
using BlazorDashboard.Services;

namespace BlazorDashboard.Charts;

public enum RateChartPlotMode
{
    PercentChangeSinceFirst,
    RawValues,
}

/// <summary>One data point: the rate on a date, its change since the series start, and its SVG position.</summary>
public sealed record ChartPoint(DateOnly Date, decimal Rate, decimal Change, double X, double Y);

/// <summary>A quote currency's line, plus where its end-of-line label goes after collision avoidance.</summary>
public sealed record ChartSeries(string Quote, IReadOnlyList<ChartPoint> Points, double LabelY)
{
    public ChartPoint Last => Points[^1];

    /// <summary>Value for an SVG polyline's <c>points</c> attribute.</summary>
    public string SvgPoints => string.Join(' ', Points.Select(p =>
        string.Create(CultureInfo.InvariantCulture, $"{p.X:0.##},{p.Y:0.##}")));
}

public sealed record AxisTick(double Position, string Label);

/// <summary>A date's hover band: the pointer anywhere in [Start, End) snaps to <see cref="X"/>.</summary>
public sealed record ChartColumn(DateOnly Date, double X, double Start, double End);

/// <summary>
/// Geometry for the "% change since start" line chart, in a fixed SVG coordinate space
/// (<see cref="Width"/> × <see cref="Height"/>) that CSS scales to fit.
/// </summary>
public sealed class RateChartLayout
{
    public const double Width = 820; // right of PlotRight is room for direct labels
    public const double Height = 320;
    public const double PlotLeft = 56;
    public const double PlotRight = 728;
    public const double PlotTop = 16;
    public const double PlotBottom = 288;
    public const double LabelGap = 16;

    private const int MaxXTicks = 5;
    public IReadOnlyList<ChartSeries> Series { get; private init; } = [];
    public IReadOnlyList<AxisTick> YTicks { get; private init; } = [];
    public IReadOnlyList<AxisTick> XTicks { get; private init; } = [];
    public IReadOnlyList<ChartColumn> Columns { get; private init; } = [];
    public double ZeroY { get; private init; }
    public bool IsEmpty => Series.Count == 0;

    public static RateChartLayout Build(
        IEnumerable<Rate> rows,
        IReadOnlyList<string> quoteOrder,
        IFormatProvider? provider = null,
        RateChartPlotMode plotMode = RateChartPlotMode.PercentChangeSinceFirst,
        bool includeZeroInYScale = true,
        Func<decimal, string>? yTickFormatter = null)
    {
        yTickFormatter ??= v => v.ToString("+0.##%;-0.##%;0%", provider);

        // Each series as (date, rate, metric) sorted by date, in quote order.
        var raw = rows.GroupBy(r => r.Quote)
            .OrderBy(g => quoteOrder.IndexOf(g.Key))
            .Select(g =>
            {
                var ordered = g.OrderBy(r => r.Date).ToList();
                var first = ordered[0].Value;
                return (Quote: g.Key, Rows: ordered.Select(r => (r.Date, r.Value, Metric: plotMode == RateChartPlotMode.PercentChangeSinceFirst
                    ? first == 0 ? 0m : r.Value / first - 1
                    : r.Value)).ToList());
            })
            .ToList();
        if (raw.Count == 0)
        {
            return new RateChartLayout();
        }

        var dates = raw.SelectMany(s => s.Rows.Select(r => r.Date)).Distinct().Order().ToList();
        var firstDay = dates[0].DayNumber;
        var spanDays = dates[^1].DayNumber - firstDay;
        double XOf(DateOnly d) => spanDays == 0
            ? (PlotLeft + PlotRight) / 2
            : PlotLeft + (double)(d.DayNumber - firstDay) / spanDays * (PlotRight - PlotLeft);

        var values = raw.SelectMany(s => s.Rows.Select(r => r.Metric)).ToList();
        var min = values.Min();
        var max = values.Max();
        if (includeZeroInYScale)
        {
            min = Math.Min(0, min);
            max = Math.Max(0, max);
        }

        var scale = NiceScale.For(min, max);
        double YOf(decimal v) => PlotTop + (double)((scale.Max - v) / (scale.Max - scale.Min)) * (PlotBottom - PlotTop);

        var series = raw.Select(s => new ChartSeries(
                s.Quote,
                s.Rows.Select(r => new ChartPoint(r.Date, r.Value, r.Metric, XOf(r.Date), YOf(r.Metric))).ToList(),
                LabelY: 0))
            .ToList();

        var xs = dates.Select(XOf).ToList();
        var columns = dates.Select((d, i) => new ChartColumn(
                d,
                xs[i],
                i == 0 ? PlotLeft : (xs[i - 1] + xs[i]) / 2,
                i == dates.Count - 1 ? PlotRight : (xs[i] + xs[i + 1]) / 2))
            .ToList();

        var dateFormat = spanDays > 365 ? "MMM yyyy" : "MMM d";
        var xTicks = Enumerable.Range(0, Math.Min(MaxXTicks, dates.Count))
            .Select(i => dates.Count == 1 ? 0 : (int)Math.Round((double)i * (dates.Count - 1) / (Math.Min(MaxXTicks, dates.Count) - 1), MidpointRounding.AwayFromZero))
            .Distinct()
            .Select(i => new AxisTick(xs[i], dates[i].ToString(dateFormat, provider)))
            .ToList();

        return new RateChartLayout
        {
            Series = SpreadLabels(series),
            YTicks = scale.Ticks.Select(t => new AxisTick(YOf(t), yTickFormatter(t))).ToList(),
            XTicks = xTicks,
            Columns = columns,
            ZeroY = YOf(0),
        };
    }

    /// <summary>Sets each series' LabelY to its end point, nudged so labels are at least LabelGap apart inside the plot.</summary>
    private static List<ChartSeries> SpreadLabels(List<ChartSeries> series)
    {
        // Stable sort by end-point height keeps series order among ties.
        var order = series.Select((s, i) => (Index: i, Y: s.Last.Y)).OrderBy(t => t.Y).ToList();
        var ys = order.Select(t => t.Y).ToArray();
        for (var i = 1; i < ys.Length; i++)
        {
            ys[i] = Math.Max(ys[i], ys[i - 1] + LabelGap);
        }
        ys[^1] = Math.Min(ys[^1], PlotBottom);
        for (var i = ys.Length - 2; i >= 0; i--)
        {
            ys[i] = Math.Min(ys[i], ys[i + 1] - LabelGap);
        }

        var result = new List<ChartSeries>(series);
        for (var i = 0; i < order.Count; i++)
        {
            result[order[i].Index] = series[order[i].Index] with { LabelY = Math.Max(ys[i], PlotTop) };
        }
        return result;
    }
}
