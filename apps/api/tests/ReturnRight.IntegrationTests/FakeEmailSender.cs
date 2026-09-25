using ReturnRight.Api.Email;

namespace ReturnRight.IntegrationTests;

/// <summary>Records sent messages; can be told to fail the next N sends.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];
    public int FailuresRemaining { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (FailuresRemaining > 0)
        {
            FailuresRemaining--;
            throw new InvalidOperationException(
                $"SMTP delivery failed for {message.ToEmail}");
        }
        Sent.Add(message);
        return Task.CompletedTask;
    }

    public void Reset()
    {
        Sent.Clear();
        FailuresRemaining = 0;
    }
}

/// <summary>Mutable clock for tests — starts at real now; tests advance as needed.</summary>
public sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetUtcNow(DateTimeOffset value) => _utcNow = value;

    public void Advance(TimeSpan delta) => _utcNow += delta;
}
