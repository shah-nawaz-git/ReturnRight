using FluentValidation;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Cases.Readiness;
using ReturnRight.Api.Persistence;
using ReturnRight.Api.Security;

namespace ReturnRight.Api.Features.Cases.NextAction;

public record DismissNextActionRequest(string? Key);

public sealed class DismissNextActionRequestValidator : AbstractValidator<DismissNextActionRequest>
{
    public DismissNextActionRequestValidator()
    {
        RuleFor(r => r.Key).NotEmpty().MaximumLength(100);
    }
}

public static class NextActionEndpoints
{
    /// <summary>Mapped on the /api/cases group.</summary>
    public static RouteGroupBuilder MapNextActionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/next-action", Get);
        group.MapPost("/{id:guid}/next-action/dismiss", Dismiss)
            .AddEndpointFilter<ValidationFilter<DismissNextActionRequest>>();
        return group;
    }

    /// <summary>First candidate not matching the dismissed key.</summary>
    public static NextActionResponse Pick(IssueCase issueCase, DateTimeOffset now)
    {
        var readiness = CaseReadinessService.Evaluate(issueCase);
        return NextActionService.Evaluate(issueCase, readiness, now)
            .First(c => c.Key != issueCase.DismissedNextActionKey);
    }

    private static async Task<IResult> Get(
        Guid id, CurrentUser current, CaseDetailBuilder builder, TimeProvider time, CancellationToken ct)
    {
        var issueCase = await builder.LoadAsync(id, current.Id, ct);
        return issueCase is null
            ? Results.NotFound()
            : Results.Ok(Pick(issueCase, time.GetUtcNow()));
    }

    private static async Task<IResult> Dismiss(
        Guid id,
        DismissNextActionRequest request,
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

        var candidates = NextActionService.Evaluate(
            issueCase, CaseReadinessService.Evaluate(issueCase), time.GetUtcNow());
        var candidate = candidates.FirstOrDefault(c => c.Key == request.Key);
        if (candidate is null || !candidate.IsDismissible)
        {
            return ProblemResults.Create(
                StatusCodes.Status400BadRequest, "That suggestion isn't available to dismiss.");
        }

        issueCase.DismissedNextActionKey = candidate.Key;
        await db.SaveChangesAsync(ct);
        return Results.Ok(Pick(issueCase, time.GetUtcNow()));
    }
}
