using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Persistence;
using ReturnRight.Api.Security;

namespace ReturnRight.Api.Features.Auth;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken });
        });

        group.MapPost("/register", Register)
            .RequireRateLimiting("auth")
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>();

        group.MapPost("/login", Login)
            .RequireRateLimiting("auth")
            .AddEndpointFilter<ValidationFilter<LoginRequest>>();

        group.MapPost("/logout", Logout).RequireAuthorization();
        group.MapGet("/me", Me).RequireAuthorization();

        return group;
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        UserManager<AppUser> users,
        SignInManager<AppUser> signIn,
        TimeProvider time)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = new AppUser { UserName = email, Email = email, CreatedAt = time.GetUtcNow() };

        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail"))
            {
                return ProblemResults.Create(
                    StatusCodes.Status409Conflict,
                    "An account with this email already exists. Try logging in instead.");
            }

            var errors = result.Errors
                .GroupBy(
                    e => e.Code.StartsWith("Password", StringComparison.Ordinal) ? "Password" : "Email",
                    e => e.Code == "PasswordTooShort" ? "Use at least 10 characters." : e.Description)
                .ToDictionary(g => g.Key, g => g.Distinct().ToArray());
            return ProblemResults.Validation(
                "Some fields need attention. Please review and try again.", errors);
        }

        await signIn.SignInAsync(user, isPersistent: true);
        return Results.Created($"/api/users/{user.Id}", ToResponse(user));
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        UserManager<AppUser> users,
        SignInManager<AppUser> signIn)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var result = await signIn.PasswordSignInAsync(
            email, request.Password, isPersistent: true, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return ProblemResults.Create(
                StatusCodes.Status401Unauthorized,
                "Too many failed attempts. Try again in a few minutes.");
        }
        if (!result.Succeeded)
        {
            return ProblemResults.Create(
                StatusCodes.Status401Unauthorized,
                "Email or password is incorrect.");
        }

        var user = await users.FindByEmailAsync(email);
        return user is null
            ? ProblemResults.Create(
                StatusCodes.Status401Unauthorized, "Email or password is incorrect.")
            : Results.Ok(ToResponse(user));
    }

    private static async Task<IResult> Logout(SignInManager<AppUser> signIn)
    {
        await signIn.SignOutAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> Me(
        CurrentUser current,
        UserManager<AppUser> users,
        AppDbContext db)
    {
        var user = await users.FindByIdAsync(current.Id.ToString());
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var unread = await db.InAppNotifications
            .CountAsync(n => n.UserId == user.Id && n.ReadAt == null);
        return Results.Ok(new MeResponse(
            user.Id, user.Email!, user.EmailRemindersEnabled, user.InAppRemindersEnabled, unread));
    }

    internal static UserResponse ToResponse(AppUser user) =>
        new(user.Id, user.Email!, user.EmailRemindersEnabled, user.InAppRemindersEnabled);
}
