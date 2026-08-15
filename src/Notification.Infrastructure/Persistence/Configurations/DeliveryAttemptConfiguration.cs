using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Notifications;

namespace Notification.Infrastructure.Persistence.Configurations;

public sealed class DeliveryAttemptConfiguration : IEntityTypeConfiguration<DeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<DeliveryAttempt> b)
    {
        b.ToTable("delivery_attempts", t =>
        {
            t.HasCheckConstraint("ck_delivery_attempts_no", "attempt_no >= 1");
            t.HasCheckConstraint("ck_delivery_attempts_result", "result IN ('success','transient_failure','permanent_failure')");
            t.HasCheckConstraint("ck_delivery_attempts_outcome", "(result = 'success' AND error_code IS NULL) OR (result <> 'success' AND error_code IS NOT NULL)");
            t.HasCheckConstraint("ck_delivery_attempts_time", "finished_at >= started_at");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.NotificationId).HasColumnName("notification_id"); b.Property(x => x.SenderId).HasColumnName("sender_id");
        b.Property(x => x.AttemptNo).HasColumnName("attempt_no"); b.Property(x => x.Result).HasColumnName("result").HasMaxLength(32);
        b.Property(x => x.ProviderMessageId).HasColumnName("provider_message_id").HasMaxLength(500); b.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(64);
        b.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(1000); b.Property(x => x.StartedAt).HasColumnName("started_at");
        b.Property(x => x.FinishedAt).HasColumnName("finished_at"); b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => new { x.NotificationId, x.AttemptNo }).IsUnique().HasDatabaseName("ux_delivery_attempts_notification_no");
        b.HasIndex(x => new { x.TenantId, x.CreatedAt }).HasDatabaseName("ix_delivery_attempts_tenant_created");
        b.HasIndex(x => new { x.TenantId, x.NotificationId }).HasDatabaseName("ix_delivery_attempts_tenant_notification");
        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Notification).WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
    }
}
