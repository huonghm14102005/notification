using FluentValidation;

namespace Notification.Api.Contracts.Identity;

public sealed record LoginRequest(string Email, string Password);
public sealed record TokenRequest(string RefreshToken);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

public sealed class TokenRequestValidator : AbstractValidator<TokenRequest>
{
    public TokenRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(256);
}
