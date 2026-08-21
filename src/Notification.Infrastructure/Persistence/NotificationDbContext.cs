using Microsoft.EntityFrameworkCore;
using Notification.Domain.Devices;
using Notification.Domain.Callbacks;
using Notification.Domain.Identity;
using Notification.Domain.Notifications;
using Notification.Domain.Senders;
using Notification.Domain.Templates;

namespace Notification.Infrastructure.Persistence;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Sender> Senders => Set<Sender>();
    public DbSet<ContentTemplate> Templates => Set<ContentTemplate>();
    public DbSet<OutboundNotification> Notifications => Set<OutboundNotification>();
    public DbSet<DeliveryAttempt> DeliveryAttempts => Set<DeliveryAttempt>();
    public DbSet<StatusEvent> StatusEvents => Set<StatusEvent>();
    public DbSet<CallbackAttempt> CallbackAttempts => Set<CallbackAttempt>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
}
