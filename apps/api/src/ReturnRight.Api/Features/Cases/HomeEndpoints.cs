using Microsoft.EntityFrameworkCore;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Persistence;
using ReturnRight.Api.Security;

namespace ReturnRight.Api.Features.Cases;

public record UpcomingFollowUpResponse(
    Guid FollowUpId,
    Guid CaseId,
    string CaseTitle,
    string Title,
    DateTimeOffset DueAt);

public record HomeResponse(
    int ActiveCaseCount,
    int FollowUpsDueTodayCount,
    int OverdueFollowUpCount,
    CaseSummaryResponse[] Attention,
    UpcomingFollowUpResponse[] UpcomingFollowUps,
    CaseSummaryResponse[] RecentCases);

public static class HomeEndpoints
{
    public static void MapHomeEndpoints(this WebApplication app) =>
        app.MapGet("/api/home", Get).RequireAuthorization();

    private static async Task<IResult> Get(
        CurrentUser current,
        AppDbContext db,
        TimeProvider time,
        CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.Date);

        var cases = await db.IssueCases
            .Include(c => c.Purchase)
            .Include(c => c.AffectedItems).ThenInclude(a => a.PurchaseItem)
            .Include(c => c.FollowUps)
            .Where(c => c.UserId == current.Id)
            .ToListAsync(ct);

        static bool IsClosed(IssueCase c) =>
            c.Status is CaseStatus.Resolved or CaseStatus.Closed;

        var active = cases.Where(c => !IsClosed(c)).ToList();
        var openFollowUps = active
            .SelectMany(c => c.FollowUps
                .Where(f => f.CompletedAt is null && f.CancelledAt is null)
                .Select(f => (Case: c, FollowUp: f)))
            .ToList();

        var attention = active
            .Where(c =>
                c.FollowUps.Any(f =>
                    f.CompletedAt is null && f.CancelledAt is null && f.DueAt < now)
                || (c.OutcomeExpectedBy is { } expected
                    && expected < today
                    && c.OutcomeCompletedAt is null))
            .OrderBy(c => c.FollowUps
                .Where(f => f.CompletedAt is null && f.CancelledAt is null)
                .Select(f => (DateTimeOffset?)f.DueAt)
                .Min() ?? c.UpdatedAt)
            .Take(5)
            .Select(c => ToSummary(c, now))
            .ToArray();

        var upcoming = openFollowUps
            .Where(x => x.FollowUp.DueAt >= now && x.FollowUp.DueAt <= now.AddDays(7))
            .OrderBy(x => x.FollowUp.DueAt)
            .Take(5)
            .Select(x => new UpcomingFollowUpResponse(
                x.FollowUp.Id,
                x.Case.Id,
                CaseTitle.Build(x.Case),
                x.FollowUp.Title,
                x.FollowUp.DueAt))
            .ToArray();

        var recent = cases
            .OrderByDescending(c => c.UpdatedAt)
            .Take(5)
            .Select(c => ToSummary(c, now))
            .ToArray();

        return Results.Ok(new HomeResponse(
            active.Count,
            openFollowUps.Count(x => DateOnly.FromDateTime(x.FollowUp.DueAt.Date) == today),
            openFollowUps.Count(x => x.FollowUp.DueAt < now),
            attention,
            upcoming,
            recent));
    }

    private static CaseSummaryResponse ToSummary(IssueCase c, DateTimeOffset now)
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
    }
}
