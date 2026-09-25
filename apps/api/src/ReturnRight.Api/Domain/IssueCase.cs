namespace ReturnRight.Api.Domain;

public class IssueCase
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? PurchaseId { get; set; }
    public ProblemType ProblemType { get; set; }
    public required string Description { get; set; }
    public DateOnly? ProblemDiscoveredOn { get; set; }
    public CaseStatus Status { get; set; }
    public RequestedOutcomeType RequestedOutcomeType { get; set; }
    public decimal? RequestedAmount { get; set; }
    public string? RequestedCurrency { get; set; }
    public DateTimeOffset? OutcomeRequestedAt { get; set; }
    public DateTimeOffset? OutcomePromisedAt { get; set; }
    public DateOnly? OutcomeExpectedBy { get; set; }
    public DateTimeOffset? OutcomeCompletedAt { get; set; }
    public FinalOutcomeType? FinalOutcomeType { get; set; }
    public decimal? FinalAmount { get; set; }
    public string? FinalCurrency { get; set; }
    public DateOnly? FinalOutcomeOn { get; set; }
    public string? FinalNote { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? DismissedNextActionKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public AppUser User { get; set; } = null!;
    public Purchase? Purchase { get; set; }
    public List<CaseAffectedItem> AffectedItems { get; set; } = [];
    public List<Evidence> Evidence { get; set; } = [];
    public List<Interaction> Interactions { get; set; } = [];
    public List<FollowUp> FollowUps { get; set; } = [];
    public List<CaseTimelineEvent> TimelineEvents { get; set; } = [];
}

public class CaseAffectedItem
{
    public Guid CaseId { get; set; }
    public Guid PurchaseItemId { get; set; }

    public IssueCase Case { get; set; } = null!;
    public PurchaseItem PurchaseItem { get; set; } = null!;
}
