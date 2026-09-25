using Microsoft.EntityFrameworkCore;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Reminders;
using ReturnRight.Api.Persistence;
using ReturnRight.Api.Security;
using ReturnRight.Api.Storage;

namespace ReturnRight.Api.Features.Cases;

public static class CaseEndpoints
{
    public static RouteGroupBuilder MapCaseEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", List);
        group.MapPost("/", Create).AddEndpointFilter<ValidationFilter<CreateCaseRequest>>();
        group.MapGet("/{id:guid}", Detail);
        group.MapPatch("/{id:guid}", Update).AddEndpointFilter<ValidationFilter<UpdateCaseRequest>>();
        group.MapPut("/{id:guid}/purchase", LinkPurchase)
            .AddEndpointFilter<ValidationFilter<LinkPurchaseRequest>>();
        group.MapDelete("/{id:guid}/purchase", UnlinkPurchase);
        group.MapPost("/{id:guid}/status", ChangeStatus)
            .AddEndpointFilter<ValidationFilter<ChangeStatusRequest>>();
        group.MapPost("/{id:guid}/resolve", Resolve)
            .AddEndpointFilter<ValidationFilter<ResolveCaseRequest>>();
        group.MapPost("/{id:guid}/reopen", Reopen);
        group.MapDelete("/{id:guid}", Delete);
        return group;
    }

    private static async Task<IResult> List(
        string? filter,
        CurrentUser current,
        AppDbContext db,
        TimeProvider time,
        CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var query = db.IssueCases
            .Include(c => c.Purchase)
            .Include(c => c.AffectedItems).ThenInclude(a => a.PurchaseItem)
            .Include(c => c.FollowUps)
            .Where(c => c.UserId == current.Id);

        query = filter switch
        {
            "resolved" => query.Where(c =>
                c.Status == CaseStatus.Resolved || c.Status == CaseStatus.Closed),
            "all" => query,
            _ => query.Where(c =>
                c.Status != CaseStatus.Resolved && c.Status != CaseStatus.Closed),
        };

        var cases = await query.ToListAsync(ct);
        var summaries = cases
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c =>
            {
                var open = c.FollowUps
                    .Where(f => f.CompletedAt is null && f.CancelledAt is null)
                    .ToList();
                return new CaseSummaryResponse(
                    c.Id,
                    CaseTitle.Build(c),
                    c.ProblemType.ToString(),
                    c.Status.ToString(),
                    c.RequestedOutcomeType.ToString(),
                    c.RequestedAmount,
                    c.RequestedCurrency,
                    c.Purchase?.MerchantName,
                    c.PurchaseId,
                    c.CreatedAt,
                    c.UpdatedAt,
                    open.Count == 0 ? null : open.Min(f => f.DueAt),
                    open.Any(f => f.DueAt < now),
                    c.OutcomeExpectedBy);
            })
            .ToArray();

        return Results.Ok(summaries);
    }

    private static async Task<IResult> Create(
        CreateCaseRequest request,
        CurrentUser current,
        AppDbContext db,
        TimeProvider time,
        CaseTimelineWriter timeline,
        CaseDetailBuilder builder,
        CancellationToken ct)
    {
        var (purchase, error) = await LoadPurchaseAsync(
            db, current.Id, request.PurchaseId, request.AffectedItemIds, ct);
        if (error is not null)
        {
            return error;
        }

        var now = time.GetUtcNow();
        var issueCase = new IssueCase
        {
            UserId = current.Id,
            PurchaseId = purchase?.Id,
            ProblemType = request.ProblemType!.Value,
            Description = request.Description!.Trim(),
            ProblemDiscoveredOn = request.ProblemDiscoveredOn,
            Status = CaseStatus.Open,
            RequestedOutcomeType = request.RequestedOutcomeType!.Value,
            RequestedAmount = request.RequestedAmount,
            RequestedCurrency = request.RequestedCurrency ??
                (request.RequestedAmount is not null ? purchase?.Currency : null),
            OutcomeRequestedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        if (purchase is not null && request.AffectedItemIds is { Length: > 0 })
        {
            foreach (var itemId in request.AffectedItemIds)
            {
                issueCase.AffectedItems.Add(new CaseAffectedItem
                {
                    CaseId = issueCase.Id,
                    PurchaseItemId = itemId,
                });
            }
        }

        db.IssueCases.Add(issueCase);
        timeline.Add(issueCase, TimelineEventType.CaseCreated, "Case created");
        if (purchase is not null)
        {
            LinkPurchaseGraph(issueCase, purchase, timeline, now);
        }

        await db.SaveChangesAsync(ct);
        var loaded = (await builder.LoadAsync(issueCase.Id, current.Id, ct))!;
        return Results.Created($"/api/cases/{issueCase.Id}", builder.ToDetail(loaded));
    }

    private static async Task<IResult> Detail(
        Guid id, CurrentUser current, CaseDetailBuilder builder, CancellationToken ct)
    {
        var issueCase = await builder.LoadAsync(id, current.Id, ct);
        return issueCase is null ? Results.NotFound() : Results.Ok(builder.ToDetail(issueCase));
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateCaseRequest request,
        CurrentUser current,
        AppDbContext db,
        TimeProvider time,
        CaseTimelineWriter timeline,
        CaseDetailBuilder builder,
        CancellationToken ct)
    {
        var issueCase = await builder.LoadAsync(id, current.Id, ct);
        if (issueCase is null)
        {
            return Results.NotFound();
        }

        var now = time.GetUtcNow();
        issueCase.ProblemType = request.ProblemType!.Value;
        issueCase.Description = request.Description!.Trim();
        if (issueCase.RequestedOutcomeType != request.RequestedOutcomeType!.Value)
        {
            issueCase.RequestedOutcomeType = request.RequestedOutcomeType.Value;
            timeline.Add(
                issueCase,
                TimelineEventType.RequestedOutcomeChanged,
                $"Requested outcome changed to {CaseLabels.Label(request.RequestedOutcomeType.Value)}");
        }
        issueCase.RequestedAmount = request.RequestedAmount;
        issueCase.RequestedCurrency = request.RequestedCurrency ??
            (request.RequestedAmount is not null ? issueCase.RequestedCurrency : null);
        issueCase.ProblemDiscoveredOn = request.ProblemDiscoveredOn;
        issueCase.UpdatedAt = now;

        await db.SaveChangesAsync(ct);
        return Results.Ok(builder.ToDetail(issueCase));
    }

    private static async Task<IResult> LinkPurchase(
        Guid id,
        LinkPurchaseRequest request,
        CurrentUser current,
        AppDbContext db,
        TimeProvider time,
        CaseTimelineWriter timeline,
        CaseDetailBuilder builder,
        CancellationToken ct)
    {
        var issueCase = await builder.LoadAsync(id, current.Id, ct);
        if (issueCase is null)
        {
            return Results.NotFound();
        }

        var purchase = await db.Purchases
            .Include(p => p.Items)
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(
                p => p.Id == request.PurchaseId!.Value && p.UserId == current.Id, ct);
        if (purchase is null)
        {
            return Results.NotFound();
        }

        var itemError = ValidateAffectedItems(request.AffectedItemIds, purchase);
        if (itemError is not null)
        {
            return itemError;
        }

        var now = time.GetUtcNow();
        issueCase.PurchaseId = purchase.Id;
        issueCase.AffectedItems.Clear();
        if (request.AffectedItemIds is { Length: > 0 })
        {
            foreach (var itemId in request.AffectedItemIds)
            {
                issueCase.AffectedItems.Add(new CaseAffectedItem
                {
                    CaseId = issueCase.Id,
                    PurchaseItemId = itemId,
                });
            }
        }

        LinkPurchaseGraph(issueCase, purchase, timeline, now);
        issueCase.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return Results.Ok(builder.ToDetail(issueCase));
    }

    private static async Task<IResult> UnlinkPurchase(
        Guid id,
        CurrentUser current,
        AppDbContext db,
        TimeProvider time,
        CaseTimelineWriter timeline,
        CaseDetailBuilder builder,
        CancellationToken ct)
    {
        var issueCase = await builder.LoadAsync(id, current.Id, ct);
        if (issueCase is null)
        {
            return Results.NotFound();
        }

        issueCase.PurchaseId = null;
        issueCase.AffectedItems.Clear();
        issueCase.UpdatedAt = time.GetUtcNow();
        timeline.Add(issueCase, TimelineEventType.PurchaseLinked, "Purchase unlinked");
        await db.SaveChangesAsync(ct);
        return Results.Ok(builder.ToDetail(issueCase));
    }

    private static async Task<IResult> ChangeStatus(
        Guid id,
        ChangeStatusRequest request,
        CurrentUser current,
        AppDbContext db,
        TimeProvider time,
        CaseTimelineWriter timeline,
        CaseDetailBuilder builder,
        CancellationToken ct)
    {
        var issueCase = await builder.LoadAsync(id, current.Id, ct);
        if (issueCase is null)
        {
            return Results.NotFound();
        }

        var status = request.Status!.Value;
        if (status is CaseStatus.Resolved or CaseStatus.Closed)
        {
            return ProblemResults.Create(
                StatusCodes.Status400BadRequest, "Use Resolve to finish a case.");
        }
        if (status == issueCase.Status)
        {
            return ProblemResults.Create(
                StatusCodes.Status400BadRequest, "The case already has this status.");
        }

        issueCase.Status = status;
        issueCase.UpdatedAt = time.GetUtcNow();
        var summary = $"Status changed to {CaseLabels.Label(status)}";
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            summary += $" — {request.Note.Trim()}";
        }
        timeline.Add(
            issueCase,
            TimelineEventType.StatusChanged,
            summary,
            string.IsNullOrWhiteSpace(request.Note) ? null : new { note = request.Note.Trim() });

        await db.SaveChangesAsync(ct);
        return Results.Ok(builder.ToDetail(issueCase));
    }

    private static async Task<IResult> Resolve(
        Guid id,
        ResolveCaseRequest request,
        CurrentUser current,
        AppDbContext db,
        TimeProvider time,
        CaseTimelineWriter timeline,
        CaseDetailBuilder builder,
        ReminderScheduler reminders,
        CancellationToken ct)
    {
        var issueCase = await builder.LoadAsync(id, current.Id, ct);
        if (issueCase is null)
        {
            return Results.NotFound();
        }
        if (issueCase.Status is CaseStatus.Resolved or CaseStatus.Closed)
        {
            return ProblemResults.Create(
                StatusCodes.Status400BadRequest, "This case is already finished.");
        }

        var outcome = request.FinalOutcomeType!.Value;
        var now = time.GetUtcNow();
        var positive = ResolveRules.IsPositiveOutcome(outcome);

        issueCase.Status = ResolveRules.StatusFor(outcome);
        issueCase.FinalOutcomeType = outcome;
        issueCase.FinalAmount = request.FinalAmount;
        issueCase.FinalCurrency = request.FinalCurrency ??
            (request.FinalAmount is not null ? issueCase.RequestedCurrency : null);
        issueCase.FinalOutcomeOn = request.CompletedOn ?? DateOnly.FromDateTime(now.DateTime);
        issueCase.FinalNote = request.Note?.Trim();
        issueCase.ResolvedAt = now;
        if (positive)
        {
            issueCase.OutcomeCompletedAt = now;
        }
        issueCase.UpdatedAt = now;

        timeline.Add(
            issueCase,
            TimelineEventType.CaseResolved,
            $"{(positive ? "Case resolved" : "Case closed")}: {CaseLabels.Label(outcome)}");

        await reminders.CancelForCaseAsync(issueCase.Id, ct);
        await db.SaveChangesAsync(ct);
        return Results.Ok(builder.ToDetail(issueCase));
    }

    private static async Task<IResult> Reopen(
        Guid id,
        CurrentUser current,
        AppDbContext db,
        TimeProvider time,
        CaseTimelineWriter timeline,
        CaseDetailBuilder builder,
        CancellationToken ct)
    {
        var issueCase = await builder.LoadAsync(id, current.Id, ct);
        if (issueCase is null)
        {
            return Results.NotFound();
        }
        if (issueCase.Status is not (CaseStatus.Resolved or CaseStatus.Closed))
        {
            return ProblemResults.Create(
                StatusCodes.Status400BadRequest, "This case isn't finished yet.");
        }

        issueCase.Status = CaseStatus.Open;
        issueCase.FinalOutcomeType = null;
        issueCase.FinalAmount = null;
        issueCase.FinalCurrency = null;
        issueCase.FinalOutcomeOn = null;
        issueCase.FinalNote = null;
        issueCase.ResolvedAt = null;
        issueCase.OutcomeCompletedAt = null;
        issueCase.UpdatedAt = time.GetUtcNow();
        timeline.Add(issueCase, TimelineEventType.CaseReopened, "Case reopened");

        await db.SaveChangesAsync(ct);
        return Results.Ok(builder.ToDetail(issueCase));
    }

    private static async Task<IResult> Delete(
        Guid id,
        CurrentUser current,
        AppDbContext db,
        IFileStorage storage,
        CancellationToken ct)
    {
        var issueCase = await db.IssueCases
            .Include(c => c.Evidence).ThenInclude(e => e.Document)
            .Include(c => c.Interactions).ThenInclude(i => i.Document)
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == current.Id, ct);
        if (issueCase is null)
        {
            return Results.NotFound();
        }

        // Evidence/interaction attachments die with the case; purchase proof stays.
        var caseFiles = issueCase.Evidence
            .Select(e => e.Document)
            .Where(d => d.Category == DocumentCategory.Evidence)
            .Concat(issueCase.Interactions
                .Select(i => i.Document)
                .Where(d => d is { Category: DocumentCategory.InteractionAttachment })!)
            .Select(d => d!)
            .ToList();

        db.IssueCases.Remove(issueCase);
        db.Documents.RemoveRange(caseFiles);
        await db.SaveChangesAsync(ct);

        foreach (var document in caseFiles)
        {
            await storage.DeleteAsync(document.StorageKey, ct);
        }
        return Results.NoContent();
    }

    private static async Task<(Purchase? Purchase, IResult? Error)> LoadPurchaseAsync(
        AppDbContext db, Guid userId, Guid? purchaseId, Guid[]? affectedItemIds, CancellationToken ct)
    {
        if (purchaseId is null)
        {
            return affectedItemIds is { Length: > 0 }
                ? (null, ProblemResults.ValidationField(
                    "affectedItemIds", "Affected items require a linked purchase."))
                : (null, null);
        }

        var purchase = await db.Purchases
            .Include(p => p.Items)
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(p => p.Id == purchaseId.Value && p.UserId == userId, ct);
        if (purchase is null)
        {
            return (null, ProblemResults.Create(StatusCodes.Status404NotFound, "Purchase not found."));
        }

        return (purchase, ValidateAffectedItems(affectedItemIds, purchase));
    }

    private static IResult? ValidateAffectedItems(Guid[]? affectedItemIds, Purchase purchase)
    {
        if (affectedItemIds is not { Length: > 0 })
        {
            return null;
        }
        var owned = purchase.Items.Select(i => i.Id).ToHashSet();
        return affectedItemIds.All(owned.Contains)
            ? null
            : ProblemResults.ValidationField(
                "affectedItemIds", "Some items don't belong to this purchase.");
    }

    /// <summary>Timeline + auto proof evidence for a (re)linked purchase.</summary>
    private static void LinkPurchaseGraph(
        IssueCase issueCase, Purchase purchase, CaseTimelineWriter timeline, DateTimeOffset now)
    {
        timeline.Add(
            issueCase,
            TimelineEventType.PurchaseLinked,
            $"Linked purchase from {purchase.MerchantName}");

        var existing = issueCase.Evidence.Select(e => e.DocumentId).ToHashSet();
        foreach (var document in purchase.Documents
                     .Where(d => d.Category == DocumentCategory.PurchaseDocument))
        {
            if (existing.Contains(document.Id))
            {
                continue;
            }
            issueCase.Evidence.Add(new Domain.Evidence
            {
                CaseId = issueCase.Id,
                DocumentId = document.Id,
                EvidenceType = EvidenceType.PurchaseProof,
                CreatedAt = now,
            });
            timeline.Add(
                issueCase,
                TimelineEventType.EvidenceAdded,
                $"Purchase proof added: {document.OriginalFileName}");
        }
    }
}
