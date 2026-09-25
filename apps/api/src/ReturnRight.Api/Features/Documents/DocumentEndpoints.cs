using Microsoft.EntityFrameworkCore;
using ReturnRight.Api.Persistence;
using ReturnRight.Api.Security;
using ReturnRight.Api.Storage;

namespace ReturnRight.Api.Features.Documents;

public static class DocumentEndpoints
{
    public static RouteGroupBuilder MapDocumentEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", (Guid id, HttpContext context,
                CurrentUser current, AppDbContext db, IFileStorage storage, CancellationToken ct) =>
            StreamAsync(id, "inline", context, current, db, storage, ct));

        group.MapGet("/{id:guid}/download", (Guid id, HttpContext context,
                CurrentUser current, AppDbContext db, IFileStorage storage, CancellationToken ct) =>
            StreamAsync(id, "attachment", context, current, db, storage, ct));

        group.MapDelete("/{id:guid}", Delete);
        return group;
    }

    private static async Task<IResult> StreamAsync(
        Guid id,
        string disposition,
        HttpContext context,
        CurrentUser current,
        AppDbContext db,
        IFileStorage storage,
        CancellationToken ct)
    {
        var document = await db.Documents
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == current.Id, ct);
        if (document is null)
        {
            return Results.NotFound();
        }

        Stream file;
        try
        {
            file = await storage.OpenReadAsync(document.StorageKey, ct);
        }
        catch (FileNotFoundException)
        {
            return Results.NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            return Results.NotFound();
        }

        var response = context.Response;
        response.Headers.ContentDisposition =
            DocumentMappings.ContentDispositionHeader(disposition, document.OriginalFileName);
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers.CacheControl = "private, no-store";
        response.Headers.ContentSecurityPolicy = "sandbox";

        return Results.File(file, document.ContentType);
    }

    private static async Task<IResult> Delete(
        Guid id, CurrentUser current, AppDbContext db, IFileStorage storage, CancellationToken ct)
    {
        var document = await db.Documents
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == current.Id, ct);
        if (document is null)
        {
            return Results.NotFound();
        }

        db.Documents.Remove(document);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(document.StorageKey, ct);
        return Results.NoContent();
    }
}
