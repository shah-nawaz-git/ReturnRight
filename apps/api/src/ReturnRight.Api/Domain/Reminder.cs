namespace ReturnRight.Api.Domain;

public class Reminder
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CaseId { get; set; }
    public Guid? FollowUpId { get; set; }
    public ReminderChannel Channel { get; set; }
    public DateTimeOffset ScheduledFor { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public ReminderStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public AppUser User { get; set; } = null!;
    public IssueCase Case { get; set; } = null!;
    public FollowUp? FollowUp { get; set; }
}
