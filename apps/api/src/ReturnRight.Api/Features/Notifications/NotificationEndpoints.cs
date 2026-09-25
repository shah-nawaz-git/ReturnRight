using Microsoft.EntityFrameworkCore;
using ReturnRight.Api.Persistence;
using ReturnRight.Api.Security;

namespace ReturnRight.Api.Features.Notifications;

public record NotificationResponse(
    Guid Id,
    string Title,
    string Body,
    Guid? CaseId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", List);
        group.MapPost("/{id:guid}/read", MarkRead);
        group.MapPost("/read-all", MarkAllRead);
        group.MapDelete("/{id:guid}", Delete);
        return group;
    }

    private static async Task<IResult> List(
        CurrentUser current, AppDbContext db, CancellationToken ct)
    {
        var notifications = await db.InAppNotifications
            .Where(n => n.UserId == current.Id)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new NotificationResponse(
                n.Id, n.Title, n.Body, n.CaseId, n.CreatedAt, n.ReadAt))
            .ToListAsync(ct);
        return Results.Ok(notifications);
    }

    private static async Task<IResult> MarkRead(
        Guid id, CurrentUser current, AppDbContext db, TimeProvider time, CancellationToken ct)
    {
        var notification = await db.InAppNotifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == current.Id, ct);
        if (notification is null)
        {
            return Results.NotFound();
        }

        notification.ReadAt ??= time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> MarkAllRead(
        CurrentUser current, AppDbContext db, TimeProvider time, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await db.InAppNotifications
            .Where(n => n.UserId == current.Id && n.ReadAt == null)
            .ExecuteUpdateAsync(
                n => n.SetProperty(x => x.ReadAt, now), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Delete(
        Guid id, CurrentUser current, AppDbContext db, CancellationToken ct)
    {
        var notification = await db.InAppNotifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == current.Id, ct);
        if (notification is null)
        {
            return Results.NotFound();
        }

        db.InAppNotifications.Remove(notification);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
