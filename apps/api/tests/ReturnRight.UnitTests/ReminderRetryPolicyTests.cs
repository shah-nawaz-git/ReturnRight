using ReturnRight.Api.Features.Reminders;

namespace ReturnRight.UnitTests;

public class ReminderRetryPolicyTests
{
    private static readonly int[] Defaults = [5, 30, 120];

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 30)]
    [InlineData(3, 120)]
    public void Default_delays_follow_the_schedule(int attempt, int expectedMinutes)
    {
        var delay = ReminderRetryPolicy.NextDelay(Defaults, attempt);
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), delay);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(10)]
    public void Exhausted_attempts_stop_retrying(int attempt)
    {
        Assert.Null(ReminderRetryPolicy.NextDelay(Defaults, attempt));
    }

    [Fact]
    public void Custom_delays_are_honoured()
    {
        var delays = new[] { 1, 2 };
        Assert.Equal(TimeSpan.FromMinutes(1), ReminderRetryPolicy.NextDelay(delays, 1));
        Assert.Equal(TimeSpan.FromMinutes(2), ReminderRetryPolicy.NextDelay(delays, 2));
        Assert.Null(ReminderRetryPolicy.NextDelay(delays, 3));
    }
}
