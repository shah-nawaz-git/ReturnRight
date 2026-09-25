namespace ReturnRight.Api.Features.Reminders;

public class ReminderOptions
{
    private static readonly int[] DefaultRetryDelays = [5, 30, 120];

    public int PollIntervalSeconds { get; set; } = 30;

    // No initializer — the configuration binder appends to initialized arrays,
    // so the defaults are applied through EffectiveRetryDelays instead.
    public int[]? RetryDelaysMinutes { get; set; }

    public int[] EffectiveRetryDelays =>
        RetryDelaysMinutes is { Length: > 0 } delays ? delays : DefaultRetryDelays;

    public int BatchSize { get; set; } = 50;
}

/// <summary>Pure retry schedule: attempt N waits RetryDelaysMinutes[N-1]; exhausted → null.</summary>
public static class ReminderRetryPolicy
{
    public static TimeSpan? NextDelay(IReadOnlyList<int> retryDelaysMinutes, int attemptCount)
    {
        if (attemptCount < 1 || attemptCount > retryDelaysMinutes.Count)
        {
            return null;
        }
        return TimeSpan.FromMinutes(retryDelaysMinutes[attemptCount - 1]);
    }
}
