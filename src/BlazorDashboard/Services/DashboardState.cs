namespace BlazorDashboard.Services;

public sealed record DashboardState(
    string BaseCode,
    decimal Amount,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<string> Quotes,
    IReadOnlyDictionary<string, int>? ColorSlots);
