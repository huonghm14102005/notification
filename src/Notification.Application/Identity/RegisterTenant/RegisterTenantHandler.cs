using Notification.Application.Abstractions.Security;
using Notification.Application.Identity.Abstractions;
using Notification.Domain.Identity;

namespace Notification.Application.Identity.RegisterTenant;

public sealed class RegisterTenantHandler(IIdentityRepository repository, IPasswordHasher passwordHasher)
{
    public async Task<RegisteredTenant> HandleAsync(RegisterTenantCommand command, CancellationToken cancellationToken)
    {
        var name = command.TenantName.Trim();
        var slug = command.TenantSlug.Trim().ToLowerInvariant();
        var email = command.AdminEmail.Trim().ToLowerInvariant();
        if (await repository.SlugExistsAsync(slug, cancellationToken)) throw new RegistrationConflictException("TENANT_SLUG_EXISTS");
        if (await repository.EmailExistsAsync(email, cancellationToken)) throw new RegistrationConflictException("ADMIN_EMAIL_EXISTS");

        var now = DateTimeOffset.UtcNow;
        var tenant = new Tenant(Guid.NewGuid(), name, slug, now);
        var admin = new Admin(Guid.NewGuid(), tenant.Id, email, passwordHasher.Hash(command.AdminPassword), now);
        await repository.AddRegistrationAsync(tenant, admin, cancellationToken);
        return new(tenant.Id, tenant.Name, tenant.Slug, admin.Id, admin.Email, admin.Role);
    }
}
