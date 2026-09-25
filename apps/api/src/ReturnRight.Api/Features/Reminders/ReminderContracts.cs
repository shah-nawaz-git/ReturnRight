using Microsoft.EntityFrameworkCore;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Persistence;
using ReturnRight.Api.Security;

namespace ReturnRight.Api.Features.Reminders;

public record ReminderResponse(
    Guid Id,
    Guid? FollowUpId,
    string Channel,
    string Status,
    DateTimeOffset ScheduledFor,
    DateTimeOffset NextAttemptAt,
    DateTimeOffset? SentAt,
    int AttemptCount,
    string? LastError);

public static class ReminderEndpoints
{
    /// <summary>Mapped on the /api/cases group.</summary>
    public static RouteGroupBuilder MapReminderEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/reminders", ListForCase);
        return group;
    }

    private static async Task<IResult> ListForCase(
        Guid id, CurrentUser current, AppDbContext db, CancellationToken ct)
    {
        var owned = await db.IssueCases
            .AnyAsync(c => c.Id == id && c.UserId == current.Id, ct);
        if (!owned)
        {
            return Results.NotFound();
        }

        var reminders = await db.Reminders
            .Where(r => r.CaseId == id)
            .OrderByDescending(r => r.ScheduledFor)
            .Select(r => new ReminderResponse(
                r.Id,
                r.FollowUpId,
                r.Channel.ToString(),
                r.Status.ToString(),
                r.ScheduledFor,
                r.NextAttemptAt,
                r.SentAt,
                r.AttemptCount,
                r.LastError))
            .ToListAsync(ct);
        return Results.Ok(reminders);
    }
}
