using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Documents;
using ReturnRight.Api.Persistence;
using ReturnRight.Api.Security;
using ReturnRight.Api.Storage;

namespace ReturnRight.Api.Features.Purchases;

public static class PurchaseEndpoints
{
    public static RouteGroupBuilder MapPurchaseEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", List);
        group.MapPost("/", Create).AddEndpointFilter<ValidationFilter<CreatePurchaseRequest>>();
        group.MapGet("/{id:guid}", Detail);
        group.MapPatch("/{id:guid}", Update).AddEndpointFilter<ValidationFilter<UpdatePurchaseRequest>>();
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/documents", UploadDocument)
            .RequireRateLimiting("uploads")
            .WithMetadata(new RequestSizeLimitAttribute(11 * 1024 * 1024));
        return group;
    }

    private static IQueryable<Purchase> ForUser(AppDbContext db, Guid userId) =>
        db.Purchases.Where(p => p.UserId == userId);

    private static async Task<IResult> List(
        CurrentUser current, AppDbContext db, CancellationToken ct)
    {
        var purchases = await ForUser(db, current.Id)
            .Include(p => p.Items)
            .Include(p => p.Documents)
            .Include(p => p.Cases)
            .ToListAsync(ct);

        var summaries = purchases
            .OrderByDescending(p => p.PurchaseDate.HasValue)
            .ThenByDescending(p => p.PurchaseDate)
            .ThenByDescending(p => p.CreatedAt)
            .Select(PurchaseMappings.ToSummaryResponse)
            .ToArray();

        return Results.Ok(summaries);
    }

    private static async Task<IResult> Create(
        CreatePurchaseRequest request,
        CurrentUser current,
        AppDbContext db,
        TimeProvider time,
        CancellationToken ct)
    {
        var now = time.GetUtcNow();
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

        var sort = 0;
        foreach (var input in request.Items!)
        {
            purchase.Items.Add(NewItem(purchase, input, sort++, null, now));
        }

        db.Purchases.Add(purchase);
        await db.SaveChangesAsync(ct);
        return Results.Created(
            $"/api/purchases/{purchase.Id}", PurchaseMappings.ToDetailResponse(purchase));
    }

    private static async Task<IResult> Detail(
        Guid id, CurrentUser current, AppDbContext db, CancellationToken ct)
    {
        var purchase = await ForUser(db, current.Id)
            .Include(p => p.Items)
            .Include(p => p.Documents)
            .Include(p => p.Cases)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return purchase is null
            ? Results.NotFound()
            : Results.Ok(PurchaseMappings.ToDetailResponse(purchase));
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdatePurchaseRequest request,
        CurrentUser current,
        AppDbContext db,
        TimeProvider time,
        CancellationToken ct)
    {
        var purchase = await ForUser(db, current.Id)
            .Include(p => p.Items)
            .Include(p => p.Documents)
            .Include(p => p.Cases)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (purchase is null)
        {
            return Results.NotFound();
        }

        var now = time.GetUtcNow();
        if (purchase.MerchantName != request.MerchantName!.Trim())
        {
            purchase.MerchantName = request.MerchantName.Trim();
            purchase.MerchantNameProvenance = FieldProvenance.User();
        }
        UpdateScalar(
            request.OrderNumber?.Trim(), purchase.OrderNumber,
            v => purchase.OrderNumber = v,
            () => purchase.OrderNumberProvenance = FieldProvenance.User());
        UpdateScalar(
            request.PurchaseDate, purchase.PurchaseDate,
            v => purchase.PurchaseDate = v,
            () => purchase.PurchaseDateProvenance = FieldProvenance.User());
        UpdateScalar(
            request.TotalAmount, purchase.TotalAmount,
            v => purchase.TotalAmount = v,
            () => purchase.TotalAmountProvenance = FieldProvenance.User());
        purchase.Currency = request.Currency!;
        purchase.Notes = request.Notes?.Trim();

        var requestedIds = request.Items!.Where(i => i.Id is not null).Select(i => i.Id!.Value).ToHashSet();
        var removed = purchase.Items.Where(i => !requestedIds.Contains(i.Id)).ToList();
        if (removed.Count > 0)
        {
            var removedIds = removed.Select(i => i.Id).ToHashSet();
            var referenced = await db.CaseAffectedItems
                .AnyAsync(a => removedIds.Contains(a.PurchaseItemId), ct);
            if (referenced)
            {
                return ProblemResults.Create(
                    StatusCodes.Status409Conflict,
                    "This item is part of a case. Remove it from the case first.");
            }
            db.PurchaseItems.RemoveRange(removed);
        }

        var sort = 0;
        foreach (var input in request.Items!)
        {
            var existing = input.Id is not null
                ? purchase.Items.FirstOrDefault(i => i.Id == input.Id)
                : null;
            if (existing is null)
            {
                var item = NewItem(purchase, input, sort++, null, now);
                db.PurchaseItems.Add(item);
            }
            else
            {
                existing.ProductName = input.ProductName!.Trim();
                existing.Quantity = input.Quantity!.Value;
                existing.UnitPrice = input.UnitPrice;
                existing.SortOrder = sort++;
                if (existing.ReturnDeadline != input.ReturnDeadline)
                {
                    existing.ReturnDeadline = input.ReturnDeadline;
                    existing.ReturnDeadlineProvenance = FieldProvenance.User();
                }
                if (existing.CommercialWarrantyEnd != input.CommercialWarrantyEnd)
                {
                    existing.CommercialWarrantyEnd = input.CommercialWarrantyEnd;
                    existing.CommercialWarrantyEndProvenance = FieldProvenance.User();
                }
            }
        }

        purchase.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return Results.Ok(PurchaseMappings.ToDetailResponse(purchase));
    }

    private static async Task<IResult> Delete(
        Guid id, CurrentUser current, AppDbContext db, IFileStorage storage, CancellationToken ct)
    {
        var purchase = await ForUser(db, current.Id)
            .Include(p => p.Documents)
            .Include(p => p.Cases)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (purchase is null)
        {
            return Results.NotFound();
        }
        if (purchase.Cases.Count > 0)
        {
            return ProblemResults.Create(
                StatusCodes.Status409Conflict,
                "This purchase is linked to a case. Delete the case first.");
        }

        var keys = purchase.Documents.Select(d => d.StorageKey).ToList();
        db.Purchases.Remove(purchase);
        await db.SaveChangesAsync(ct);
        foreach (var key in keys)
        {
            await storage.DeleteAsync(key, ct);
        }
        return Results.NoContent();
    }

    private static async Task<IResult> UploadDocument(
        Guid id,
        HttpContext context,
        CurrentUser current,
        AppDbContext db,
        IFileStorage storage,
        IConfiguration configuration,
        CancellationToken ct)
    {
        var purchase = await ForUser(db, current.Id)
            .Include(p => p.Documents)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (purchase is null)
        {
            return Results.NotFound();
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

        var key = await storage.SaveAsync(buffer, validation.Extension, ct);
        var now = DateTimeOffset.UtcNow;
        var document = new Document
        {
            UserId = current.Id,
            PurchaseId = purchase.Id,
            StorageKey = key,
            OriginalFileName = validation.SafeFileName,
            ContentType = validation.ContentType,
            SizeBytes = file.Length,
            Category = DocumentCategory.PurchaseDocument,
            CreatedAt = now,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/documents/{document.Id}", DocumentMappings.ToResponse(document));
    }

    private static PurchaseItem NewItem(
        Purchase purchase, PurchaseItemInput input, int sortOrder, Guid? sourceDocumentId, DateTimeOffset now) =>
        new()
        {
            PurchaseId = purchase.Id,
            ProductName = input.ProductName!.Trim(),
            Quantity = input.Quantity!.Value,
            UnitPrice = input.UnitPrice,
            ReturnDeadline = input.ReturnDeadline,
            ReturnDeadlineProvenance =
                PurchaseMappings.ItemFieldProvenance(input.ReturnDeadlineSource, sourceDocumentId),
            CommercialWarrantyEnd = input.CommercialWarrantyEnd,
            CommercialWarrantyEndProvenance =
                PurchaseMappings.ItemFieldProvenance(input.CommercialWarrantyEndSource, sourceDocumentId),
            SortOrder = sortOrder,
            CreatedAt = now,
        };

    private static void UpdateScalar<T>(
        T? incoming, T? existing, Action<T?> set, Action markUser)
    {
        if (!EqualityComparer<T?>.Default.Equals(incoming, existing))
        {
            set(incoming);
            markUser();
        }
    }
}
