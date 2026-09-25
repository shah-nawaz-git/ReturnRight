using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Cases;
using ReturnRight.Api.Features.Documents;
using ReturnRight.Api.Persistence;
using ReturnRight.Api.Security;
using ReturnRight.Api.Storage;

namespace ReturnRight.Api.Features.Interactions;

public record CreateInteractionRequest(
    InteractionType? InteractionType,
    DateTimeOffset? OccurredAt,
    string? Note,
    DateOnly? ExpectedBy,
    decimal? PromisedAmount);

public record UpdateInteractionRequest(
    InteractionType? InteractionType,
    DateTimeOffset? OccurredAt,
    string? Note);

public sealed class CreateInteractionRequestValidator : AbstractValidator<CreateInteractionRequest>
{
    public CreateInteractionRequestValidator(TimeProvider time)
    {
        RuleFor(r => r.InteractionType).NotNull();
        RuleFor(r => r.OccurredAt).NotNull()
            .LessThanOrEqualTo(_ => time.GetUtcNow().AddMinutes(5))
            .WithMessage("The date can't be in the future.");
        RuleFor(r => r.Note).NotEmpty().MaximumLength(1000);
        RuleFor(r => r.PromisedAmount).GreaterThanOrEqualTo(0)
            .When(r => r.PromisedAmount is not null);
    }
}

public sealed class UpdateInteractionRequestValidator : AbstractValidator<UpdateInteractionRequest>
{
    public UpdateInteractionRequestValidator(TimeProvider time)
    {
        RuleFor(r => r.InteractionType).NotNull();
        RuleFor(r => r.OccurredAt).NotNull()
            .LessThanOrEqualTo(_ => time.GetUtcNow().AddMinutes(5))
            .WithMessage("The date can't be in the future.");
        RuleFor(r => r.Note).NotEmpty().MaximumLength(1000);
    }
}

public static class InteractionEndpoints
{
    /// <summary>Mapped on the /api/cases group.</summary>
    public static RouteGroupBuilder MapInteractionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/interactions", Create)
            .AddEndpointFilter<ValidationFilter<CreateInteractionRequest>>();
        group.MapPatch("/{id:guid}/interactions/{interactionId:guid}", Update)
            .AddEndpointFilter<ValidationFilter<UpdateInteractionRequest>>();
        group.MapDelete("/{id:guid}/interactions/{interactionId:guid}", Delete);
        group.MapPost("/{id:guid}/interactions/{interactionId:guid}/attachment", UploadAttachment)
            .RequireRateLimiting("uploads")
            .WithMetadata(new RequestSizeLimitAttribute(11 * 1024 * 1024));
        group.MapDelete("/{id:guid}/interactions/{interactionId:guid}/attachment", DeleteAttachment);
        return group;
    }

    private static InteractionResponse ToResponse(Interaction interaction) =>
        new(
            interaction.Id,
            interaction.InteractionType.ToString(),
            interaction.OccurredAt,
            interaction.Note,
            interaction.Document is null ? null : DocumentMappings.ToResponse(interaction.Document),
            interaction.CreatedAt);

    private static async Task<IResult> Create(
        Guid id,
        CreateInteractionRequest request,
        CurrentUser current,
        AppDbContext db,
        TimeProvider time,
        CaseDetailBuilder builder,
        CaseTimelineWriter timeline,
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
        var note = request.Note!.Trim();
        var interaction = new Interaction
        {
            CaseId = issueCase.Id,
            InteractionType = request.InteractionType!.Value,
            OccurredAt = request.OccurredAt!.Value,
            Note = note,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Interactions.Add(interaction);

        timeline.Add(
            issueCase,
            TimelineEventType.InteractionAdded,
            CaseLabels.Label(interaction.InteractionType),
            new { note = note.Length > 120 ? note[..120] : note });

        foreach (var (type, summary) in InteractionRules.Apply(
                     issueCase, interaction, request.ExpectedBy, now))
        {
            timeline.Add(issueCase, type, summary);
        }

        issueCase.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return Results.Created(
            $"/api/cases/{id}/interactions/{interaction.Id}", ToResponse(interaction));
    }

    private static async Task<IResult> Update(
        Guid id,
        Guid interactionId,
        UpdateInteractionRequest request,
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
        if (CaseGuards.RejectFinished(issueCase) is { } blocked)
        {
            return blocked;
        }
        var interaction = issueCase.Interactions.FirstOrDefault(i => i.Id == interactionId);
        if (interaction is null)
        {
            return Results.NotFound();
        }

        interaction.InteractionType = request.InteractionType!.Value;
        interaction.OccurredAt = request.OccurredAt!.Value;
        interaction.Note = request.Note!.Trim();
        interaction.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToResponse(interaction));
    }

    private static async Task<IResult> Delete(
        Guid id,
        Guid interactionId,
        CurrentUser current,
        AppDbContext db,
        IFileStorage storage,
        TimeProvider time,
        CaseDetailBuilder builder,
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
        var interaction = issueCase.Interactions.FirstOrDefault(i => i.Id == interactionId);
        if (interaction is null)
        {
            return Results.NotFound();
        }

        var document = interaction.Document;
        db.Interactions.Remove(interaction);
        if (document is not null)
        {
            db.Documents.Remove(document);
        }
        issueCase.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);

        if (document is not null)
        {
            await storage.DeleteAsync(document.StorageKey, ct);
        }
        return Results.NoContent();
    }

    private static async Task<IResult> UploadAttachment(
        Guid id,
        Guid interactionId,
        HttpContext context,
        CurrentUser current,
        AppDbContext db,
        IFileStorage storage,
        IConfiguration configuration,
        TimeProvider time,
        CaseDetailBuilder builder,
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
        var interaction = issueCase.Interactions.FirstOrDefault(i => i.Id == interactionId);
        if (interaction is null)
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

        var now = time.GetUtcNow();
        var key = await storage.SaveAsync(buffer, validation.Extension, ct);
        var document = new Document
        {
            UserId = current.Id,
            StorageKey = key,
            OriginalFileName = validation.SafeFileName,
            ContentType = validation.ContentType,
            SizeBytes = buffer.Length,
            Category = DocumentCategory.InteractionAttachment,
            CreatedAt = now,
        };
        db.Documents.Add(document);

        var replaced = interaction.Document;
        interaction.DocumentId = document.Id;
        interaction.Document = document;
        interaction.UpdatedAt = now;
        if (replaced is not null)
        {
            db.Documents.Remove(replaced);
        }
        await db.SaveChangesAsync(ct);

        if (replaced is not null)
        {
            await storage.DeleteAsync(replaced.StorageKey, ct);
        }
        return Results.Ok(ToResponse(interaction));
    }

    private static async Task<IResult> DeleteAttachment(
        Guid id,
        Guid interactionId,
        CurrentUser current,
        AppDbContext db,
        IFileStorage storage,
        TimeProvider time,
        CaseDetailBuilder builder,
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
        var interaction = issueCase.Interactions.FirstOrDefault(i => i.Id == interactionId);
        if (interaction is null)
        {
            return Results.NotFound();
        }

        var document = interaction.Document;
        if (document is not null)
        {
            interaction.DocumentId = null;
            db.Documents.Remove(document);
            interaction.UpdatedAt = time.GetUtcNow();
            await db.SaveChangesAsync(ct);
            await storage.DeleteAsync(document.StorageKey, ct);
        }
        return Results.NoContent();
    }
}
