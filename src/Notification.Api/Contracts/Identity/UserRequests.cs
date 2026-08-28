using FluentValidation;

namespace Notification.Api.Contracts.Identity;

public sealed record CreateUserRequest(string Email, string Password, string? DisplayName);
public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.DisplayName).MaximumLength(100).Must(x => x is null || !string.IsNullOrWhiteSpace(x));
    }
}
