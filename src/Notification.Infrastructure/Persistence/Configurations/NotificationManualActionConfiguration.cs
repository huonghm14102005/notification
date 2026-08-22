using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Notifications;

namespace Notification.Infrastructure.Persistence.Configurations;

public sealed class NotificationManualActionConfiguration : IEntityTypeConfiguration<NotificationManualAction>
{
    public void Configure(EntityTypeBuilder<NotificationManualAction> b)
    {
        b.ToTable("notification_manual_actions", t => t.HasCheckConstraint("ck_notification_manual_actions_action", "action IN ('retry','cancel')"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.AdminId).HasColumnName("admin_id"); b.Property(x => x.SourceNotificationId).HasColumnName("source_notification_id");
        b.Property(x => x.ResultNotificationId).HasColumnName("result_notification_id"); b.Property(x => x.Action).HasColumnName("action").HasMaxLength(16);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => new { x.TenantId, x.SourceNotificationId, x.Action }).IsUnique().HasDatabaseName("ux_notification_manual_actions_source_action");
        b.HasIndex(x => new { x.TenantId, x.CreatedAt, x.Id }).HasDatabaseName("ix_notification_manual_actions_tenant_created");
        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Admin).WithMany().HasForeignKey(x => x.AdminId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SourceNotification).WithMany().HasForeignKey(x => x.SourceNotificationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ResultNotification).WithMany().HasForeignKey(x => x.ResultNotificationId).OnDelete(DeleteBehavior.Restrict);
    }
}
