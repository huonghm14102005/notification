using Microsoft.EntityFrameworkCore;
using Notification.Application.Identity.Abstractions;
using Notification.Application.Identity.RegisterTenant;
using Notification.Domain.Identity;
using Npgsql;

namespace Notification.Infrastructure.Persistence;

public sealed class IdentityRepository(NotificationDbContext dbContext) : IIdentityRepository
{
    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct) => dbContext.Tenants.AnyAsync(x => x.Slug == slug && x.DeletedAt == null, ct);
    public Task<bool> EmailExistsAsync(string email, CancellationToken ct) => dbContext.Admins.AnyAsync(x => x.Email == email && x.DeletedAt == null, ct);
    public async Task AddRegistrationAsync(Tenant tenant, Admin admin, CancellationToken ct)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        dbContext.Tenants.Add(tenant);
        dbContext.Admins.Add(admin);
        try
        {
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
        {
            throw postgres.ConstraintName switch
            {
                "ux_tenants_slug_active" => new RegistrationConflictException("TENANT_SLUG_EXISTS"),
                "ux_admins_email_active" => new RegistrationConflictException("ADMIN_EMAIL_EXISTS"),
                _ => exception,
            };
        }
    }
}
