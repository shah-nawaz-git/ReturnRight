using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Auth;
using ReturnRight.Api.Persistence;
using ReturnRight.Api.Security;
using ReturnRight.Api.Storage;

namespace ReturnRight.Api.Features.Profile;

public record UpdateProfileRequest(bool? EmailRemindersEnabled, bool? InAppRemindersEnabled);

public record DeleteAccountRequest(string? Password);

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(r => r.EmailRemindersEnabled).NotNull();
        RuleFor(r => r.InAppRemindersEnabled).NotNull();
    }
}

public sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        RuleFor(r => r.Password).NotEmpty();
    }
}

public static class ProfileEndpoints
{
    public static RouteGroupBuilder MapProfileEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", Get);
        group.MapPatch("/", Update).AddEndpointFilter<ValidationFilter<UpdateProfileRequest>>();
        group.MapDelete("/", Delete).AddEndpointFilter<ValidationFilter<DeleteAccountRequest>>();
        return group;
    }

    private static async Task<IResult> Get(
        CurrentUser current, AppDbContext db, CancellationToken ct)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == current.Id, ct);
        return user is null ? Results.NotFound() : Results.Ok(AuthEndpoints.ToResponse(user));
    }

    private static async Task<IResult> Update(
        UpdateProfileRequest request,
        CurrentUser current,
        AppDbContext db,
        CancellationToken ct)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == current.Id, ct);
        if (user is null)
        {
            return Results.NotFound();
        }

        user.EmailRemindersEnabled = request.EmailRemindersEnabled!.Value;
        user.InAppRemindersEnabled = request.InAppRemindersEnabled!.Value;
        await db.SaveChangesAsync(ct);
        return Results.Ok(AuthEndpoints.ToResponse(user));
    }

    private static async Task<IResult> Delete(
        [FromBody] DeleteAccountRequest request,
        CurrentUser current,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        AppDbContext db,
        IFileStorage storage,
        CancellationToken ct)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Id == current.Id, ct);
        if (user is null)
        {
            return Results.NotFound();
        }

        var passwordOk = await userManager.CheckPasswordAsync(user, request.Password!);
        if (!passwordOk)
        {
            return ProblemResults.Create(
                StatusCodes.Status401Unauthorized, "Your password is incorrect.");
        }

        var storageKeys = await db.Documents
            .Where(d => d.UserId == user.Id)
            .Select(d => d.StorageKey)
            .Concat(db.TemporaryIntakes
                .Where(t => t.UserId == user.Id)
                .Select(t => t.StorageKey))
            .ToListAsync(ct);

        // FK cascade removes purchases, cases, documents, intakes, reminders,
        // and notifications with the user.
        await userManager.DeleteAsync(user);
        await signInManager.SignOutAsync();

        foreach (var key in storageKeys)
        {
            await storage.DeleteAsync(key, ct);
        }
        return Results.NoContent();
    }
}
