using Microsoft.EntityFrameworkCore;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Documents;
using ReturnRight.Api.Features.Purchases;
using ReturnRight.Api.Persistence;

namespace ReturnRight.Api.Features.Cases;

public class CaseDetailBuilder(AppDbContext db, TimeProvider time)
{
    public async Task<IssueCase?> LoadAsync(Guid id, Guid userId, CancellationToken ct) =>
        await db.IssueCases
            .Include(c => c.Purchase!).ThenInclude(p => p.Items)
            .Include(c => c.Purchase!).ThenInclude(p => p.Documents)
            .Include(c => c.AffectedItems).ThenInclude(a => a.PurchaseItem)
            .Include(c => c.Evidence).ThenInclude(e => e.Document)
            .Include(c => c.Interactions).ThenInclude(i => i.Document)
            .Include(c => c.FollowUps)
            .Include(c => c.TimelineEvents)
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

    public CaseDetailResponse ToDetail(IssueCase issueCase)
    {
        var now = time.GetUtcNow();
        var readiness = Readiness.CaseReadinessService.Evaluate(issueCase);
        var nextAction = NextAction.NextActionService.Evaluate(issueCase, readiness, now)
            .First(c => c.Key != issueCase.DismissedNextActionKey);
        return new CaseDetailResponse(
            issueCase.Id,
            CaseTitle.Build(issueCase),
            issueCase.ProblemType.ToString(),
            issueCase.Description,
            issueCase.ProblemDiscoveredOn,
            issueCase.Status.ToString(),
            issueCase.RequestedOutcomeType.ToString(),
            issueCase.RequestedAmount,
            issueCase.RequestedCurrency,
            issueCase.OutcomeRequestedAt,
            issueCase.OutcomePromisedAt,
            issueCase.OutcomeExpectedBy,
            issueCase.OutcomeCompletedAt,
            issueCase.FinalOutcomeType is null
                ? null
                : new FinalOutcomeResponse(
                    issueCase.FinalOutcomeType.Value.ToString(),
                    issueCase.FinalAmount,
                    issueCase.FinalCurrency,
                    issueCase.FinalOutcomeOn,
                    issueCase.FinalNote),
            issueCase.ResolvedAt,
            issueCase.Purchase is null ? null : ToCasePurchase(issueCase.Purchase),
            issueCase.AffectedItems
                .OrderBy(a => a.PurchaseItem?.SortOrder ?? 0)
                .Select(a => a.PurchaseItemId)
                .ToArray(),
            issueCase.Evidence
                .OrderBy(e => e.CreatedAt)
                .Select(e => new EvidenceResponse(
                    e.Id,
                    e.EvidenceType.ToString(),
                    e.Description,
                    DocumentMappings.ToResponse(e.Document),
                    e.CreatedAt))
                .ToArray(),
            issueCase.Interactions
                .OrderByDescending(i => i.OccurredAt)
                .Select(i => new InteractionResponse(
                    i.Id,
                    i.InteractionType.ToString(),
                    i.OccurredAt,
                    i.Note,
                    i.Document is null ? null : DocumentMappings.ToResponse(i.Document),
                    i.CreatedAt))
                .ToArray(),
            issueCase.FollowUps
                .OrderBy(f => f.DueAt)
                .Select(f => new FollowUpResponse(
                    f.Id,
                    f.Title,
                    f.DueAt,
                    f.CompletedAt,
                    f.CancelledAt,
                    f.CompletedAt is null && f.CancelledAt is null && f.DueAt < now))
                .ToArray(),
            issueCase.TimelineEvents
                .OrderBy(e => e.OccurredAt)
                .ThenBy(e => e.CreatedAt)
                .Select(e => new TimelineEventResponse(
                    e.Id, e.EventType.ToString(), e.OccurredAt, e.Summary))
                .ToArray(),
            readiness,
            nextAction,
            issueCase.CreatedAt,
            issueCase.UpdatedAt);
    }

    private static CasePurchaseResponse ToCasePurchase(Purchase purchase) =>
        new(
            purchase.Id,
            purchase.MerchantName,
            purchase.OrderNumber,
            purchase.PurchaseDate,
            purchase.Currency,
            purchase.TotalAmount,
            purchase.Items
                .OrderBy(i => i.SortOrder)
                .Select(PurchaseMappings.ToResponse)
                .ToArray(),
            purchase.Documents
                .OrderBy(d => d.CreatedAt)
                .Select(DocumentMappings.ToResponse)
                .ToArray());
}
