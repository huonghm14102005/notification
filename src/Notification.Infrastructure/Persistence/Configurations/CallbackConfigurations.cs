using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Callbacks;

namespace Notification.Infrastructure.Persistence.Configurations;

public sealed class StatusEventConfiguration : IEntityTypeConfiguration<StatusEvent>
{
    public void Configure(EntityTypeBuilder<StatusEvent> builder)
    {
        builder.ToTable("status_events", table =>
        {
            table.HasCheckConstraint("ck_status_events_status", "status IN ('pending','sending','delivered','failed','cancelled')");
            table.HasCheckConstraint("ck_status_events_attempt_count", "attempt_count BETWEEN 0 AND 6");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.PublicId).HasColumnName("public_id").HasMaxLength(36).IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id"); builder.Property(x => x.DeviceId).HasColumnName("device_id");
        builder.Property(x => x.NotificationId).HasColumnName("notification_id"); builder.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(64).IsRequired();
        builder.Property(x => x.PayloadEncrypted).HasColumnName("payload_encrypted").IsRequired(); builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count"); builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(x => x.FailureCode).HasColumnName("failure_code").HasMaxLength(64); builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at"); builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(x => x.PublicId).IsUnique().HasDatabaseName("ux_status_events_public_id");
        builder.HasIndex(x => new { x.NotificationId, x.EventType }).IsUnique().HasDatabaseName("ux_status_events_notification_type");
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt }).HasDatabaseName("ix_status_events_status_due");
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Notification).WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CallbackAttemptConfiguration : IEntityTypeConfiguration<CallbackAttempt>
{
    public void Configure(EntityTypeBuilder<CallbackAttempt> builder)
    {
        builder.ToTable("callback_attempts", table =>
        {
            table.HasCheckConstraint("ck_callback_attempts_no", "attempt_no BETWEEN 1 AND 6");
            table.HasCheckConstraint("ck_callback_attempts_result", "result IN ('success','transient_failure','permanent_failure')");
            table.HasCheckConstraint("ck_callback_attempts_error", "(result = 'success' AND error_code IS NULL) OR (result <> 'success' AND error_code IS NOT NULL)");
            table.HasCheckConstraint("ck_callback_attempts_time", "finished_at >= started_at");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.EventId).HasColumnName("event_id"); builder.Property(x => x.AttemptNo).HasColumnName("attempt_no");
        builder.Property(x => x.Result).HasColumnName("result").HasMaxLength(32).IsRequired(); builder.Property(x => x.HttpStatusCode).HasColumnName("http_status_code");
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(64); builder.Property(x => x.StartedAt).HasColumnName("started_at");
        builder.Property(x => x.FinishedAt).HasColumnName("finished_at"); builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => new { x.EventId, x.AttemptNo }).IsUnique().HasDatabaseName("ux_callback_attempts_event_no");
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
    }
}
