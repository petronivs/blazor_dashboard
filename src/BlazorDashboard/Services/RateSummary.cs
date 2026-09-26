namespace BlazorDashboard.Services;

/// <summary>First and last rate for one quote currency over a date range.</summary>
public sealed record RateSummary(string Quote, Rate First, Rate Last)
{
    /// <summary>Relative change from <see cref="First"/> to <see cref="Last"/> (0.05 = +5%).</summary>
    public decimal Change => First.Value == 0 ? 0 : Last.Value / First.Value - 1;

    /// <summary>
    /// Groups rate rows by quote currency and summarizes each series.
    /// Results follow <paramref name="quoteOrder"/>; quotes not in it come first.
    /// </summary>
    public static IReadOnlyList<RateSummary> FromRates(IEnumerable<Rate> rows, IReadOnlyList<string> quoteOrder) =>
        rows.GroupBy(r => r.Quote)
            .Select(g =>
            {
                var ordered = g.OrderBy(r => r.Date).ToList();
                return new RateSummary(g.Key, ordered[0], ordered[^1]);
            })
            .OrderBy(s => quoteOrder.IndexOf(s.Quote))
            .ToList();
}
