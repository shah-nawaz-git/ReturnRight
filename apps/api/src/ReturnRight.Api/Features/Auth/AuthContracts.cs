using FluentValidation;

namespace ReturnRight.Api.Features.Auth;

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);

public record UserResponse(
    Guid Id,
    string Email,
    bool EmailRemindersEnabled,
    bool InAppRemindersEnabled);

public record MeResponse(
    Guid Id,
    string Email,
    bool EmailRemindersEnabled,
    bool InAppRemindersEnabled,
    int UnreadNotifications);

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().EmailAddress();
        RuleFor(r => r.Password).NotEmpty().MinimumLength(10)
            .WithMessage("Use at least 10 characters.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().EmailAddress();
        RuleFor(r => r.Password).NotEmpty();
    }
}
