namespace BlazorDashboard.Tests.TestSupport;

/// <summary>TimeProvider pinned to a single instant, in UTC.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
