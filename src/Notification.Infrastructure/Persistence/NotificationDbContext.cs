using Microsoft.EntityFrameworkCore;
using Notification.Domain.Identity;

namespace Notification.Infrastructure.Persistence;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
}
