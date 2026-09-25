using ReturnRight.Api.Security;

namespace ReturnRight.Api.Features.Cases.Readiness;

public static class ReadinessEndpoints
{
    /// <summary>Mapped on the /api/cases group.</summary>
    public static RouteGroupBuilder MapReadinessEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/readiness", Get);
        return group;
    }

    private static async Task<IResult> Get(
        Guid id, CurrentUser current, CaseDetailBuilder builder, CancellationToken ct)
    {
        var issueCase = await builder.LoadAsync(id, current.Id, ct);
        return issueCase is null
            ? Results.NotFound()
            : Results.Ok(CaseReadinessService.Evaluate(issueCase));
    }
}
