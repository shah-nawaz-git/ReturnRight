using ReturnRight.Api.Features.Documents;
using ReturnRight.Api.Security;

namespace ReturnRight.Api.Features.Exports;

public static class CaseFileEndpoints
{
    /// <summary>Mapped on the /api/cases group.</summary>
    public static RouteGroupBuilder MapCaseFileEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/case-file", Get);
        return group;
    }

    private static async Task<IResult> Get(
        Guid id,
        HttpContext context,
        CurrentUser current,
        CaseFileGenerator generator,
        CancellationToken ct)
    {
        var file = await generator.GenerateAsync(id, current.Id, ct);
        if (file is null)
        {
            return Results.NotFound();
        }

        context.Response.Headers.ContentDisposition =
            DocumentMappings.ContentDispositionHeader("attachment", file.FileName);
        context.Response.Headers.CacheControl = "no-store";
        return Results.File(file.Content, "application/pdf", file.FileName);
    }
}
