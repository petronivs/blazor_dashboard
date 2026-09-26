namespace BlazorDashboard.Charts;

/// <summary>An axis range widened to round numbers, with an evenly spaced tick step.</summary>
public readonly record struct NiceScale(decimal Min, decimal Max, decimal Step)
{
    /// <summary>
    /// Picks a step of 1, 2, 2.5 or 5 × 10ⁿ giving about <paramref name="targetTicks"/> ticks,
    /// then widens [min, max] out to multiples of it. A flat range is padded first so it has height.
    /// </summary>
    public static NiceScale For(decimal min, decimal max, int targetTicks = 5)
    {
        if (min > max)
        {
            throw new ArgumentException($"min ({min}) must not exceed max ({max}).", nameof(min));
        }
        if (min == max)
        {
            var pad = min == 0 ? 0.01m : Math.Abs(min) * 0.1m;
            (min, max) = (min - pad, max + pad);
        }

        var rawStep = (max - min) / (targetTicks - 1);
        var magnitude = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)rawStep)));
        var residual = rawStep / magnitude;
        var nice = residual switch
        {
            <= 1m => 1m,
            <= 2m => 2m,
            <= 2.5m => 2.5m,
            <= 5m => 5m,
            _ => 10m,
        };
        var step = nice * magnitude;

        return new NiceScale(Math.Floor(min / step) * step, Math.Ceiling(max / step) * step, step);
    }

    public IEnumerable<decimal> Ticks
    {
        get
        {
            for (var t = Min; t <= Max; t += Step)
            {
                yield return t;
            }
        }
    }
}
