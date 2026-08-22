using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Alerts;

namespace Notification.Infrastructure.Persistence.Configurations;

public sealed class FailureIncidentConfiguration : IEntityTypeConfiguration<FailureIncident>
{
    public void Configure(EntityTypeBuilder<FailureIncident> b)
    {
        b.ToTable("failure_incidents", t => t.HasCheckConstraint("ck_failure_incidents_count", "occurrence_count > 0")); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.TenantId).HasColumnName("tenant_id"); b.Property(x => x.WindowStart).HasColumnName("window_start"); b.Property(x => x.WindowEnd).HasColumnName("window_end");
        b.Property(x => x.Component).HasColumnName("component").HasMaxLength(32); b.Property(x => x.Channel).HasColumnName("channel").HasMaxLength(32); b.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(64); b.Property(x => x.SampleMessage).HasColumnName("sample_message").HasMaxLength(300);
        b.Property(x => x.FirstSeenAt).HasColumnName("first_seen_at"); b.Property(x => x.LastSeenAt).HasColumnName("last_seen_at"); b.Property(x => x.OccurrenceCount).HasColumnName("occurrence_count"); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => new { x.TenantId, x.WindowStart, x.Component, x.Channel, x.ErrorCode }).IsUnique().HasDatabaseName("ux_failure_incidents_fingerprint"); b.HasIndex(x => new { x.TenantId, x.WindowEnd, x.Id }).HasDatabaseName("ix_failure_incidents_tenant_window");
        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FailureAlertConfiguration : IEntityTypeConfiguration<FailureAlert>
{
    public void Configure(EntityTypeBuilder<FailureAlert> b)
    {
        b.ToTable("failure_alerts", t => t.HasCheckConstraint("ck_failure_alerts_status", "status IN ('pending','sending','delivered','partially_delivered','failed')")); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.TenantId).HasColumnName("tenant_id"); b.Property(x => x.WindowStart).HasColumnName("window_start"); b.Property(x => x.WindowEnd).HasColumnName("window_end"); b.Property(x => x.Status).HasColumnName("status").HasMaxLength(24); b.Property(x => x.AttemptCount).HasColumnName("attempt_count"); b.Property(x => x.RecipientCount).HasColumnName("recipient_count"); b.Property(x => x.SuccessCount).HasColumnName("success_count"); b.Property(x => x.FailureCode).HasColumnName("failure_code").HasMaxLength(64); b.Property(x => x.StartedAt).HasColumnName("started_at"); b.Property(x => x.FinishedAt).HasColumnName("finished_at"); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => new { x.TenantId, x.WindowStart }).IsUnique().HasDatabaseName("ux_failure_alerts_tenant_window"); b.HasIndex(x => new { x.Status, x.WindowEnd, x.CreatedAt, x.Id }).HasDatabaseName("ix_failure_alerts_due"); b.HasIndex(x => new { x.TenantId, x.CreatedAt, x.Id }).HasDatabaseName("ix_failure_alerts_tenant_created"); b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
