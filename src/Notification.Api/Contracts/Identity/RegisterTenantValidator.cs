using FluentValidation;

namespace Notification.Api.Contracts.Identity;

public sealed class RegisterTenantValidator : AbstractValidator<RegisterTenantRequest>
{
    public RegisterTenantValidator()
    {
        RuleFor(x => x.TenantName).NotEmpty().Length(2, 200);
        RuleFor(x => x.TenantSlug).NotEmpty().Length(3, 63).Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(x => x.AdminEmail).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.AdminPassword).NotEmpty().Length(8, 128);
    }
}
