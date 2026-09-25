using FluentValidation;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Cases.NextAction;
using ReturnRight.Api.Features.Cases.Readiness;
using ReturnRight.Api.Features.Documents;
using ReturnRight.Api.Features.Purchases;

namespace ReturnRight.Api.Features.Cases;

public record CreateCaseRequest(
    ProblemType? ProblemType,
    string? Description,
    RequestedOutcomeType? RequestedOutcomeType,
    decimal? RequestedAmount,
    string? RequestedCurrency,
    Guid? PurchaseId,
    Guid[]? AffectedItemIds,
    DateOnly? ProblemDiscoveredOn);

public record UpdateCaseRequest(
    ProblemType? ProblemType,
    string? Description,
    RequestedOutcomeType? RequestedOutcomeType,
    decimal? RequestedAmount,
    string? RequestedCurrency,
    DateOnly? ProblemDiscoveredOn);

public record LinkPurchaseRequest(Guid? PurchaseId, Guid[]? AffectedItemIds);

public record ChangeStatusRequest(CaseStatus? Status, string? Note);

public record ResolveCaseRequest(
    FinalOutcomeType? FinalOutcomeType,
    decimal? FinalAmount,
    string? FinalCurrency,
    DateOnly? CompletedOn,
    string? Note);

public record CaseSummaryResponse(
    Guid Id,
    string Title,
    string ProblemType,
    string Status,
    string RequestedOutcomeType,
    decimal? RequestedAmount,
    string? RequestedCurrency,
    string? MerchantName,
    Guid? PurchaseId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? NextFollowUpAt,
    bool HasOverdueFollowUp,
    DateOnly? OutcomeExpectedBy);

public record FinalOutcomeResponse(
    string Type,
    decimal? Amount,
    string? Currency,
    DateOnly? On,
    string? Note);

public record CasePurchaseResponse(
    Guid Id,
    string MerchantName,
    string? OrderNumber,
    DateOnly? PurchaseDate,
    string Currency,
    decimal? TotalAmount,
    PurchaseItemResponse[] Items,
    DocumentResponse[] Documents);

public record EvidenceResponse(
    Guid Id,
    string EvidenceType,
    string? Description,
    DocumentResponse Document,
    DateTimeOffset CreatedAt);

public record InteractionResponse(
    Guid Id,
    string InteractionType,
    DateTimeOffset OccurredAt,
    string Note,
    DocumentResponse? Document,
    DateTimeOffset CreatedAt);

public record FollowUpResponse(
    Guid Id,
    string Title,
    DateTimeOffset DueAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    bool IsOverdue);

public record TimelineEventResponse(
    Guid Id,
    string EventType,
    DateTimeOffset OccurredAt,
    string Summary);

public record CaseDetailResponse(
    Guid Id,
    string Title,
    string ProblemType,
    string Description,
    DateOnly? ProblemDiscoveredOn,
    string Status,
    string RequestedOutcomeType,
    decimal? RequestedAmount,
    string? RequestedCurrency,
    DateTimeOffset? OutcomeRequestedAt,
    DateTimeOffset? OutcomePromisedAt,
    DateOnly? OutcomeExpectedBy,
    DateTimeOffset? OutcomeCompletedAt,
    FinalOutcomeResponse? FinalOutcome,
    DateTimeOffset? ResolvedAt,
    CasePurchaseResponse? Purchase,
    Guid[] AffectedItemIds,
    EvidenceResponse[] Evidence,
    InteractionResponse[] Interactions,
    FollowUpResponse[] FollowUps,
    TimelineEventResponse[] Timeline,
    CaseReadinessResponse Readiness,
    NextActionResponse NextAction,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class CreateCaseRequestValidator : AbstractValidator<CreateCaseRequest>
{
    public CreateCaseRequestValidator()
    {
        RuleFor(r => r.ProblemType).NotNull();
        RuleFor(r => r.Description).NotEmpty().MaximumLength(4000);
        RuleFor(r => r.RequestedOutcomeType).NotNull();
        RuleFor(r => r.RequestedAmount).GreaterThanOrEqualTo(0).When(r => r.RequestedAmount is not null);
        RuleFor(r => r.RequestedCurrency).Matches("^[A-Z]{3}$")
            .WithMessage("Currency must be a 3-letter code like EUR or USD.")
            .When(r => r.RequestedCurrency is not null);
    }
}

public sealed class UpdateCaseRequestValidator : AbstractValidator<UpdateCaseRequest>
{
    public UpdateCaseRequestValidator()
    {
        RuleFor(r => r.ProblemType).NotNull();
        RuleFor(r => r.Description).NotEmpty().MaximumLength(4000);
        RuleFor(r => r.RequestedOutcomeType).NotNull();
        RuleFor(r => r.RequestedAmount).GreaterThanOrEqualTo(0).When(r => r.RequestedAmount is not null);
        RuleFor(r => r.RequestedCurrency).Matches("^[A-Z]{3}$")
            .WithMessage("Currency must be a 3-letter code like EUR or USD.")
            .When(r => r.RequestedCurrency is not null);
    }
}

public sealed class LinkPurchaseRequestValidator : AbstractValidator<LinkPurchaseRequest>
{
    public LinkPurchaseRequestValidator()
    {
        RuleFor(r => r.PurchaseId).NotNull();
    }
}

public sealed class ChangeStatusRequestValidator : AbstractValidator<ChangeStatusRequest>
{
    public ChangeStatusRequestValidator()
    {
        RuleFor(r => r.Status).NotNull();
        RuleFor(r => r.Note).MaximumLength(500);
    }
}

public sealed class ResolveCaseRequestValidator : AbstractValidator<ResolveCaseRequest>
{
    public ResolveCaseRequestValidator()
    {
        RuleFor(r => r.FinalOutcomeType).NotNull();
        RuleFor(r => r.FinalAmount).GreaterThanOrEqualTo(0).When(r => r.FinalAmount is not null);
        RuleFor(r => r.FinalCurrency).Matches("^[A-Z]{3}$")
            .WithMessage("Currency must be a 3-letter code like EUR or USD.")
            .When(r => r.FinalCurrency is not null);
        RuleFor(r => r.Note).MaximumLength(2000);
    }
}
