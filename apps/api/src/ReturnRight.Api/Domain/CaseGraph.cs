namespace ReturnRight.Api.Domain;

public class Evidence
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid DocumentId { get; set; }
    public EvidenceType EvidenceType { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public IssueCase Case { get; set; } = null!;
    public Document Document { get; set; } = null!;
}

public class Interaction
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public InteractionType InteractionType { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public required string Note { get; set; }
    public Guid? DocumentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public IssueCase Case { get; set; } = null!;
    public Document? Document { get; set; }
}

public class FollowUp
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public required string Title { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public IssueCase Case { get; set; } = null!;
}
