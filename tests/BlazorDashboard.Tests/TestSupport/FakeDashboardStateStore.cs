using BlazorDashboard.Services;

namespace BlazorDashboard.Tests.TestSupport;

internal sealed class FakeDashboardStateStore : IDashboardStateStore
{
    public DashboardState? LoadedState { get; set; }

    public List<DashboardState> SavedStates { get; } = [];

    public ValueTask<DashboardState?> LoadAsync() => ValueTask.FromResult(LoadedState);

    public ValueTask SaveAsync(DashboardState state)
    {
        SavedStates.Add(state);
        return ValueTask.CompletedTask;
    }
}
