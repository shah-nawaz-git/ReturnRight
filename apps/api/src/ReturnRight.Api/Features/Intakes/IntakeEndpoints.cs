using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Extraction;
using ReturnRight.Api.Features.Purchases;
using ReturnRight.Api.Persistence;
using ReturnRight.Api.Security;
using ReturnRight.Api.Storage;

namespace ReturnRight.Api.Features.Intakes;

public static class IntakeEndpoints
{
    private const string FailedMessage =
        "We couldn't read this document automatically. You can still enter the purchase details manually.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static RouteGroupBuilder MapIntakeEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", Upload)
            .RequireRateLimiting("uploads")
            .WithMetadata(new RequestSizeLimitAttribute(11 * 1024 * 1024));
        group.MapGet("/{id:guid}", Detail);
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/confirm", Confirm)
            .AddEndpointFilter<ValidationFilter<ConfirmIntakeRequest>>();
        return group;
    }

    private static async Task<IResult> Upload(
        HttpContext context,
        CurrentUser current,
        AppDbContext db,
        IFileStorage storage,
        IExtractorClient extractor,
        IConfiguration configuration,
        TimeProvider time,
        CancellationToken ct)
    {
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

        var key = await storage.SaveAsync(buffer, validation.Extension, ct);
        var now = time.GetUtcNow();
        var intake = new TemporaryIntake
        {
            UserId = current.Id,
            StorageKey = key,
            OriginalFileName = validation.SafeFileName,
            ContentType = validation.ContentType,
            SizeBytes = buffer.Length,
            Status = IntakeStatus.Processing,
            ExpiresAt = now.AddHours(24),
            CreatedAt = now,
        };
        db.TemporaryIntakes.Add(intake);

        buffer.Position = 0;
        ExtractionOutcome outcome;
        try
        {
            outcome = await extractor.ExtractAsync(
                buffer, validation.SafeFileName, validation.ContentType, ct);
        }
        catch (Exception)
        {
            outcome = new ExtractionOutcome("failed", false, 0, [], [], "unavailable");
        }

        if (outcome.Status is "succeeded" or "partial")
        {
            intake.Status = IntakeStatus.Succeeded;
            intake.UsedOcr = outcome.UsedOcr;
            intake.CandidatesJson = JsonSerializer.Serialize(outcome.Candidates, JsonOptions);
            intake.ItemsJson = JsonSerializer.Serialize(outcome.Items, JsonOptions);
        }
        else
        {
            intake.Status = IntakeStatus.Failed;
            intake.UsedOcr = outcome.UsedOcr;
            intake.FailureMessage = FailedMessage;
        }

        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/intakes/{intake.Id}", ToResponse(intake));
    }

    private static async Task<IResult> Detail(
        Guid id, CurrentUser current, AppDbContext db, CancellationToken ct)
    {
        var intake = await db.TemporaryIntakes
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == current.Id, ct);
        return intake is null
            ? Results.NotFound()
            : Results.Ok(ToResponse(intake));
    }

    private static async Task<IResult> Delete(
        Guid id, CurrentUser current, AppDbContext db, IFileStorage storage, CancellationToken ct)
    {
        var intake = await db.TemporaryIntakes
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == current.Id, ct);
        if (intake is null)
        {
            return Results.NotFound();
        }

        db.TemporaryIntakes.Remove(intake);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(intake.StorageKey, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Confirm(
        Guid id,
        ConfirmIntakeRequest request,
        CurrentUser current,
        AppDbContext db,
        TimeProvider time,
        CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var intake = await db.TemporaryIntakes
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == current.Id, ct);
        if (intake is null || intake.ExpiresAt < now)
        {
            return Results.NotFound();
        }

        var purchase = new Purchase
        {
            UserId = current.Id,
            MerchantName = request.MerchantName!.Trim(),
            OrderNumber = request.OrderNumber?.Trim(),
            PurchaseDate = request.PurchaseDate,
            Currency = request.Currency!,
            TotalAmount = request.TotalAmount,
            Notes = request.Notes?.Trim(),
            MerchantNameProvenance = FieldProvenance.User(),
            OrderNumberProvenance = FieldProvenance.User(),
            PurchaseDateProvenance = FieldProvenance.User(),
            TotalAmountProvenance = FieldProvenance.User(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Purchases.Add(purchase);
        await db.SaveChangesAsync(ct); // assign purchase.Id before linking the document

        var document = new Document
        {
            UserId = current.Id,
            PurchaseId = purchase.Id,
            StorageKey = intake.StorageKey,
            OriginalFileName = intake.OriginalFileName,
            ContentType = intake.ContentType,
            SizeBytes = intake.SizeBytes,
            Category = DocumentCategory.PurchaseDocument,
            CreatedAt = now,
        };
        db.Documents.Add(document); // PurchaseId fixup links it to purchase.Documents

        purchase.MerchantNameProvenance =
            ResolveProvenance(request.Provenance?.MerchantName, document.Id);
        purchase.OrderNumberProvenance =
            ResolveProvenance(request.Provenance?.OrderNumber, document.Id);
        purchase.PurchaseDateProvenance =
            ResolveProvenance(request.Provenance?.PurchaseDate, document.Id);
        purchase.TotalAmountProvenance =
            ResolveProvenance(request.Provenance?.TotalAmount, document.Id);

        var sort = 0;
        foreach (var input in request.Items!)
        {
            purchase.Items.Add(new PurchaseItem
            {
                PurchaseId = purchase.Id,
                ProductName = input.ProductName!.Trim(),
                Quantity = input.Quantity!.Value,
                UnitPrice = input.UnitPrice,
                ReturnDeadline = input.ReturnDeadline,
                ReturnDeadlineProvenance = PurchaseMappings.ItemFieldProvenance(
                    input.ReturnDeadlineSource, document.Id),
                CommercialWarrantyEnd = input.CommercialWarrantyEnd,
                CommercialWarrantyEndProvenance = PurchaseMappings.ItemFieldProvenance(
                    input.CommercialWarrantyEndSource, document.Id),
                SortOrder = sort++,
                CreatedAt = now,
            });
        }

        db.TemporaryIntakes.Remove(intake); // the file's ownership moves to the Document
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/purchases/{purchase.Id}", PurchaseMappings.ToDetailResponse(purchase));
    }

    private static FieldProvenance ResolveProvenance(ProvenanceInput? input, Guid documentId) =>
        input?.Source == FieldSource.ExtractedFromDocument
            ? FieldProvenance.Extracted(input.Confidence, documentId, confirmed: true)
            : FieldProvenance.User();

    private static IntakeResponse ToResponse(TemporaryIntake intake) =>
        new(
            intake.Id,
            intake.Status.ToString().ToLowerInvariant(),
            intake.OriginalFileName,
            intake.ContentType,
            intake.SizeBytes,
            intake.ExpiresAt,
            intake.UsedOcr,
            Deserialize<FieldCandidate>(intake.CandidatesJson),
            Deserialize<ItemCandidate>(intake.ItemsJson),
            intake.FailureMessage);

    private static List<T> Deserialize<T>(string? json) =>
        string.IsNullOrEmpty(json)
            ? []
            : JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
}
