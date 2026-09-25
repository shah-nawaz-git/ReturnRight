using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Cases;
using ReturnRight.Api.Features.Reminders;
using ReturnRight.Api.Persistence;
using ReturnRight.Api.Security;

namespace ReturnRight.Api.Features.FollowUps;

public record CreateFollowUpRequest(string? Title, DateTimeOffset? DueAt);

public record UpdateFollowUpRequest(string? Title, DateTimeOffset? DueAt);

public sealed class CreateFollowUpRequestValidator : AbstractValidator<CreateFollowUpRequest>
{
    public CreateFollowUpRequestValidator(TimeProvider time)
    {
        RuleFor(r => r.Title).NotEmpty().MaximumLength(200);
        RuleFor(r => r.DueAt).NotNull()
            .GreaterThanOrEqualTo(_ => time.GetUtcNow())
            .WithMessage("Pick a follow-up date in the future.");
    }
}

public sealed class UpdateFollowUpRequestValidator : AbstractValidator<UpdateFollowUpRequest>
{
    public UpdateFollowUpRequestValidator(TimeProvider time)
    {
        RuleFor(r => r.Title).NotEmpty().MaximumLength(200);
        RuleFor(r => r.DueAt).NotNull()
            .GreaterThanOrEqualTo(_ => time.GetUtcNow())
            .WithMessage("Pick a follow-up date in the future.");
    }
}

public static class FollowUpEndpoints
{
    /// <summary>Mapped on the /api/cases group.</summary>
    public static RouteGroupBuilder MapFollowUpEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/follow-ups", Create)
            .AddEndpointFilter<ValidationFilter<CreateFollowUpRequest>>();
        group.MapPatch("/{id:guid}/follow-ups/{followUpId:guid}", Update)
            .AddEndpointFilter<ValidationFilter<UpdateFollowUpRequest>>();
        group.MapPost("/{id:guid}/follow-ups/{followUpId:guid}/complete", Complete);
        group.MapPost("/{id:guid}/follow-ups/{followUpId:guid}/cancel", Cancel);
        return group;
    }

    private static FollowUpResponse ToResponse(FollowUp followUp, DateTimeOffset now) =>
        new(
            followUp.Id,
            followUp.Title,
            followUp.DueAt,
            followUp.CompletedAt,
            followUp.CancelledAt,
            followUp.CompletedAt is null && followUp.CancelledAt is null && followUp.DueAt < now);

    private static async Task<IResult> Create(
        Guid id,
        CreateFollowUpRequest request,
        CurrentUser current,
        AppDbContext db,
        TimeProvider time,
        CaseDetailBuilder builder,
        CaseTimelineWriter timeline,
        ReminderScheduler scheduler,
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

        var now = time.GetUtcNow();
        var followUp = new FollowUp
        {
            CaseId = issueCase.Id,
            Title = request.Title!.Trim(),
            DueAt = request.DueAt!.Value,
            CreatedAt = now,
        };
        db.FollowUps.Add(followUp);

        var user = await db.Users.FirstAsync(u => u.Id == current.Id, ct);
        await scheduler.ScheduleForFollowUpAsync(followUp, user, ct);

        timeline.Add(
            issueCase,
            TimelineEventType.FollowUpCreated,
            $"Follow-up scheduled for {followUp.DueAt:d MMM yyyy}: {followUp.Title}");
        issueCase.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/cases/{id}/follow-ups/{followUp.Id}", ToResponse(followUp, now));
    }

    private static async Task<IResult> Update(
        Guid id,
        Guid followUpId,
        UpdateFollowUpRequest request,
        CurrentUser current,
        AppDbContext db,
        TimeProvider time,
        CaseDetailBuilder builder,
        CaseTimelineWriter timeline,
        ReminderScheduler scheduler,
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
        var followUp = issueCase.FollowUps.FirstOrDefault(f => f.Id == followUpId);
        if (followUp is null)
        {
            return Results.NotFound();
        }
        if (followUp.CompletedAt is not null || followUp.CancelledAt is not null)
        {
            return ProblemResults.Create(
                StatusCodes.Status400BadRequest, "This follow-up is already finished.");
        }

        var now = time.GetUtcNow();
        followUp.Title = request.Title!.Trim();
        if (followUp.DueAt != request.DueAt!.Value)
        {
            followUp.DueAt = request.DueAt.Value;
            await scheduler.CancelForFollowUpAsync(followUp.Id, ct);
            var user = await db.Users.FirstAsync(u => u.Id == current.Id, ct);
            await scheduler.ScheduleForFollowUpAsync(followUp, user, ct);
            timeline.Add(
                issueCase,
                TimelineEventType.FollowUpRescheduled,
                $"Follow-up moved to {followUp.DueAt:d MMM yyyy}");
        }

        issueCase.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToResponse(followUp, now));
    }

    private static async Task<IResult> Complete(
        Guid id, Guid followUpId, CurrentUser current, AppDbContext db, TimeProvider time,
        CaseDetailBuilder builder, CaseTimelineWriter timeline, ReminderScheduler scheduler,
        CancellationToken ct) =>
        await FinishAsync(id, followUpId, complete: true, current, db, time, builder, timeline, scheduler, ct);

    private static async Task<IResult> Cancel(
        Guid id, Guid followUpId, CurrentUser current, AppDbContext db, TimeProvider time,
        CaseDetailBuilder builder, CaseTimelineWriter timeline, ReminderScheduler scheduler,
        CancellationToken ct) =>
        await FinishAsync(id, followUpId, complete: false, current, db, time, builder, timeline, scheduler, ct);

    private static async Task<IResult> FinishAsync(
        Guid id,
        Guid followUpId,
        bool complete,
        CurrentUser current,
        AppDbContext db,
        TimeProvider time,
        CaseDetailBuilder builder,
        CaseTimelineWriter timeline,
        ReminderScheduler scheduler,
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
        var followUp = issueCase.FollowUps.FirstOrDefault(f => f.Id == followUpId);
        if (followUp is null)
        {
            return Results.NotFound();
        }
        if (followUp.CompletedAt is not null || followUp.CancelledAt is not null)
        {
            return ProblemResults.Create(
                StatusCodes.Status400BadRequest, "This follow-up is already finished.");
        }

        var now = time.GetUtcNow();
        if (complete)
        {
            followUp.CompletedAt = now;
        }
        else
        {
            followUp.CancelledAt = now;
        }
        await scheduler.CancelForFollowUpAsync(followUp.Id, ct);
        timeline.Add(
            issueCase,
            complete ? TimelineEventType.FollowUpCompleted : TimelineEventType.FollowUpCancelled,
            $"{(complete ? "Follow-up done" : "Follow-up cancelled")}: {followUp.Title}");

        issueCase.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToResponse(followUp, now));
    }
}
