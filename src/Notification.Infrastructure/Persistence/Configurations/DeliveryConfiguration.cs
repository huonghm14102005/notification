using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Notifications;

namespace Notification.Infrastructure.Persistence.Configurations;

public sealed class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> b)
    {
        b.ToTable("deliveries", t =>
        {
            t.HasCheckConstraint("ck_deliveries_channel", "channel = 'email'");
            t.HasCheckConstraint("ck_deliveries_status", "status IN ('pending','sending','delivered','failed','cancelled')");
            t.HasCheckConstraint("ck_deliveries_attempt_count", "attempt_count BETWEEN 0 AND 4");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.NotificationId).HasColumnName("notification_id"); b.Property(x => x.SenderId).HasColumnName("sender_id");
        b.Property(x => x.Channel).HasColumnName("channel").HasMaxLength(32); b.Property(x => x.Target).HasColumnName("target").HasMaxLength(2048);
        b.Property(x => x.TargetRef).HasColumnName("target_ref").HasMaxLength(200); b.Property(x => x.Status).HasColumnName("status").HasMaxLength(24);
        b.Property(x => x.AttemptCount).HasColumnName("attempt_count"); b.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        b.Property(x => x.FailureCode).HasColumnName("failure_code").HasMaxLength(64); b.Property(x => x.DeliveredAt).HasColumnName("delivered_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => new { x.Status, x.NextAttemptAt, x.CreatedAt, x.Id }).HasDatabaseName("ix_deliveries_status_due");
        b.HasIndex(x => new { x.TenantId, x.NotificationId }).HasDatabaseName("ix_deliveries_tenant_notification");
        b.HasIndex(x => new { x.NotificationId, x.Channel, x.Target }).IsUnique().HasDatabaseName("ux_deliveries_notification_channel_target");
        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Notification).WithMany(x => x.Deliveries).HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
    }
}
