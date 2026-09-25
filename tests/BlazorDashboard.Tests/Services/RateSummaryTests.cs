using BlazorDashboard.Services;

namespace BlazorDashboard.Tests.Services;

public class RateSummaryTests
{
    private static Rate R(int day, string quote, decimal value) => new(new DateOnly(2026, 9, day), "USD", quote, value);

    [Fact]
    public void FromRates_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(RateSummary.FromRates([], ["EUR"]));
    }

    [Fact]
    public void FromRates_PicksEarliestAndLatestByDate_RegardlessOfInputOrder()
    {
        var rows = new[] { R(3, "EUR", 0.87m), R(1, "EUR", 0.85m), R(5, "EUR", 0.88m), R(2, "EUR", 0.90m) };

        var summary = Assert.Single(RateSummary.FromRates(rows, ["EUR"]));

        Assert.Equal("EUR", summary.Quote);
        Assert.Equal(R(1, "EUR", 0.85m), summary.First);
        Assert.Equal(R(5, "EUR", 0.88m), summary.Last);
    }

    [Fact]
    public void FromRates_SingleRow_IsBothFirstAndLast()
    {
        var summary = Assert.Single(RateSummary.FromRates([R(1, "EUR", 0.85m)], ["EUR"]));

        Assert.Same(summary.First, summary.Last);
        Assert.Equal(0m, summary.Change);
    }

    [Fact]
    public void FromRates_OrdersByQuoteList()
    {
        var rows = new[] { R(1, "EUR", 1m), R(1, "JPY", 1m), R(1, "GBP", 1m) };

        var quotes = RateSummary.FromRates(rows, ["GBP", "JPY", "EUR"]).Select(s => s.Quote);

        Assert.Equal(["GBP", "JPY", "EUR"], quotes);
    }

    [Fact]
    public void FromRates_QuotesMissingFromOrderListComeFirst()
    {
        var rows = new[] { R(1, "EUR", 1m), R(1, "CHF", 1m) };

        var quotes = RateSummary.FromRates(rows, ["EUR"]).Select(s => s.Quote);

        Assert.Equal(["CHF", "EUR"], quotes);
    }

    [Theory]
    [InlineData(100, 105, 0.05)]
    [InlineData(100, 95, -0.05)]
    [InlineData(0.8, 0.8, 0)]
    [InlineData(150, 148.5, -0.01)]
    public void Change_IsRelativeDifference(double first, double last, double expected)
    {
        var summary = new RateSummary("EUR", R(1, "EUR", (decimal)first), R(2, "EUR", (decimal)last));

        Assert.Equal((decimal)expected, summary.Change);
    }

    [Fact]
    public void Change_IsZeroWhenFirstRateIsZero()
    {
        var summary = new RateSummary("EUR", R(1, "EUR", 0m), R(2, "EUR", 1m));

        Assert.Equal(0m, summary.Change);
    }
}
