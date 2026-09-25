namespace ReturnRight.Api.Domain;

public class InAppNotification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? CaseId { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public AppUser User { get; set; } = null!;
    public IssueCase? Case { get; set; }
}

public class CaseTimelineEvent
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public TimelineEventType EventType { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public required string Summary { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public IssueCase Case { get; set; } = null!;
}
