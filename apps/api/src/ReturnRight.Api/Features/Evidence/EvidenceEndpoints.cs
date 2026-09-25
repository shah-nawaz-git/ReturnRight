using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Cases;
using ReturnRight.Api.Features.Documents;
using ReturnRight.Api.Persistence;
using ReturnRight.Api.Security;
using ReturnRight.Api.Storage;

namespace ReturnRight.Api.Features.Evidence;

public record UpdateEvidenceRequest(EvidenceType? EvidenceType, string? Description);

public sealed class UpdateEvidenceRequestValidator : AbstractValidator<UpdateEvidenceRequest>
{
    public UpdateEvidenceRequestValidator()
    {
        RuleFor(r => r.EvidenceType).NotNull();
        RuleFor(r => r.Description).MaximumLength(500);
    }
}

public static class EvidenceEndpoints
{
    /// <summary>Mapped on the /api/cases group.</summary>
    public static RouteGroupBuilder MapEvidenceEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/evidence", Upload)
            .RequireRateLimiting("uploads")
            .WithMetadata(new RequestSizeLimitAttribute(11 * 1024 * 1024));
        group.MapPatch("/{id:guid}/evidence/{evidenceId:guid}", Update)
            .AddEndpointFilter<ValidationFilter<UpdateEvidenceRequest>>();
        group.MapDelete("/{id:guid}/evidence/{evidenceId:guid}", Delete);
        return group;
    }

    private static EvidenceResponse ToResponse(Domain.Evidence evidence) =>
        new(
            evidence.Id,
            evidence.EvidenceType.ToString(),
            evidence.Description,
            DocumentMappings.ToResponse(evidence.Document),
            evidence.CreatedAt);

    public static string AddedSummary(EvidenceType type, string fileName) => type switch
    {
        EvidenceType.DamagePhoto or EvidenceType.ProductPhoto => $"Photo added: {fileName}",
        EvidenceType.SellerCommunication => $"Seller message added: {fileName}",
        EvidenceType.DeliveryTracking => $"Tracking evidence added: {fileName}",
        EvidenceType.ReturnConfirmation or EvidenceType.RefundConfirmation =>
            $"Confirmation added: {fileName}",
        EvidenceType.PurchaseProof => $"Purchase proof added: {fileName}",
        _ => $"File added: {fileName}",
    };

    private static async Task<IResult> Upload(
        Guid id,
        HttpContext context,
        CurrentUser current,
        AppDbContext db,
        IFileStorage storage,
        IConfiguration configuration,
        TimeProvider time,
        CaseDetailBuilder builder,
        CaseTimelineWriter timeline,
        CancellationToken ct)
    {
        var issueCase = await builder.LoadAsync(id, current.Id, ct);
        if (issueCase is null)
        {
            return Results.NotFound();
        }
        if (CaseGuards.RejectFinished(issueCase) is { } blocked)
        {
            return blocked;
        }

        var typeValue = context.Request.Form["evidenceType"].ToString();
        if (!Enum.TryParse<EvidenceType>(typeValue, ignoreCase: true, out var evidenceType))
        {
            return ProblemResults.ValidationField(
                "evidenceType", "Pick the kind of evidence you're adding.");
        }
        var description = context.Request.Form["description"].ToString();
        if (description.Length > 500)
        {
            return ProblemResults.ValidationField(
                "description", "Keep the description under 500 characters.");
        }

        var file = context.Request.Form.Files.GetFile("file");
        if (file is null)
        {
            return ProblemResults.ValidationField("file", "Attach a file to upload.");
        }
        var (validation, buffer) = await UploadHelper.ValidateAsync(file, configuration, ct);
        if (!validation.IsValid)
        {
            return ProblemResults.ValidationField("file", validation.Error!);
        }

        var now = time.GetUtcNow();
        var key = await storage.SaveAsync(buffer, validation.Extension, ct);
        var document = new Document
        {
            UserId = current.Id,
            StorageKey = key,
            OriginalFileName = validation.SafeFileName,
            ContentType = validation.ContentType,
            SizeBytes = buffer.Length,
            Category = DocumentCategory.Evidence,
            CreatedAt = now,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync(ct); // need document.Id for the evidence row

        var evidence = new Domain.Evidence
        {
            CaseId = issueCase.Id,
            DocumentId = document.Id,
            EvidenceType = evidenceType,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Document = document,
            CreatedAt = now,
        };
        db.Evidences.Add(evidence);
        timeline.Add(
            issueCase,
            TimelineEventType.EvidenceAdded,
            AddedSummary(evidenceType, validation.SafeFileName));
        issueCase.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/cases/{id}/evidence/{evidence.Id}", ToResponse(evidence));
    }

    private static async Task<IResult> Update(
        Guid id,
        Guid evidenceId,
        UpdateEvidenceRequest request,
        CurrentUser current,
        AppDbContext db,
        TimeProvider time,
        CaseDetailBuilder builder,
        CancellationToken ct)
    {
        var issueCase = await builder.LoadAsync(id, current.Id, ct);
        if (issueCase is null)
        {
            return Results.NotFound();
        }
        if (CaseGuards.RejectFinished(issueCase) is { } blocked)
        {
            return blocked;
        }
        var evidence = issueCase.Evidence.FirstOrDefault(e => e.Id == evidenceId);
        if (evidence is null)
        {
            return Results.NotFound();
        }

        evidence.EvidenceType = request.EvidenceType!.Value;
        evidence.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        issueCase.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToResponse(evidence));
    }

    private static async Task<IResult> Delete(
        Guid id,
        Guid evidenceId,
        CurrentUser current,
        AppDbContext db,
        IFileStorage storage,
        TimeProvider time,
        CaseDetailBuilder builder,
        CaseTimelineWriter timeline,
        CancellationToken ct)
    {
        var issueCase = await builder.LoadAsync(id, current.Id, ct);
        if (issueCase is null)
        {
            return Results.NotFound();
        }
        if (CaseGuards.RejectFinished(issueCase) is { } blocked)
        {
            return blocked;
        }
        var evidence = issueCase.Evidence.FirstOrDefault(e => e.Id == evidenceId);
        if (evidence is null)
        {
            return Results.NotFound();
        }

        var document = evidence.Document;
        timeline.Add(
            issueCase,
            TimelineEventType.EvidenceRemoved,
            $"Removed {document.OriginalFileName}");

        db.Evidences.Remove(evidence);
        // Only documents that exist purely as case evidence are removed with it;
        // purchase-proof documents stay with the purchase.
        if (document.Category == DocumentCategory.Evidence)
        {
            db.Documents.Remove(document);
        }
        issueCase.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);

        if (document.Category == DocumentCategory.Evidence)
        {
            await storage.DeleteAsync(document.StorageKey, ct);
        }
        return Results.NoContent();
    }
}
