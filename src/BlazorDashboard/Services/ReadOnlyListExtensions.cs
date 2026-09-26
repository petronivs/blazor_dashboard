namespace BlazorDashboard.Services;

public static class ReadOnlyListExtensions
{
    /// <summary>Index of <paramref name="value"/> in <paramref name="list"/>, or -1 if absent.</summary>
    public static int IndexOf<T>(this IReadOnlyList<T> list, T value)
    {
        var comparer = EqualityComparer<T>.Default;
        for (var i = 0; i < list.Count; i++)
        {
            if (comparer.Equals(list[i], value))
            {
                return i;
            }
        }
        return -1;
    }
}
