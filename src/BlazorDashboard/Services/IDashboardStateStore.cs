namespace BlazorDashboard.Services;

public interface IDashboardStateStore
{
    ValueTask<DashboardState?> LoadAsync();

    ValueTask SaveAsync(DashboardState state);
}
