using Microsoft.EntityFrameworkCore;
using Notification.Domain.Identity;
using Notification.Domain.Senders;

namespace Notification.Infrastructure.Persistence;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Sender> Senders => Set<Sender>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
}
