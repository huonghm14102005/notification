using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Notifications;

namespace Notification.Infrastructure.Persistence.Configurations;

public sealed class OutboundNotificationConfiguration : IEntityTypeConfiguration<OutboundNotification>
{
    public void Configure(EntityTypeBuilder<OutboundNotification> b)
    {
        b.ToTable("notifications", t =>
        {
            t.HasCheckConstraint("ck_notifications_status", "status IN ('accepted','sending','sent','failed','cancelled')");
            t.HasCheckConstraint("ck_notifications_attempt_count", "attempt_count >= 0");
            t.HasCheckConstraint("ck_notifications_ciphertext", "octet_length(subject_encrypted) > 0 AND octet_length(body_encrypted) > 0");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.ApiKeyId).HasColumnName("api_key_id"); b.Property(x => x.SenderId).HasColumnName("sender_id");
        b.Property(x => x.TemplateId).HasColumnName("template_id"); b.Property(x => x.RecipientEmail).HasColumnName("recipient_email").HasMaxLength(254);
        b.Property(x => x.RecipientRef).HasColumnName("recipient_ref").HasMaxLength(200); b.Property(x => x.SubjectEncrypted).HasColumnName("subject_encrypted");
        b.Property(x => x.BodyEncrypted).HasColumnName("body_encrypted"); b.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
        b.Property(x => x.AttemptCount).HasColumnName("attempt_count"); b.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        b.Property(x => x.FailureReason).HasColumnName("failure_reason").HasMaxLength(1000); b.Property(x => x.SentAt).HasColumnName("sent_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => new { x.TenantId, x.CreatedAt }).HasDatabaseName("ix_notifications_tenant_created");
        b.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("ix_notifications_tenant_status");
        b.HasIndex(x => new { x.Status, x.NextAttemptAt }).HasDatabaseName("ix_notifications_status_next_attempt");
        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ApiKey).WithMany().HasForeignKey(x => x.ApiKeyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
    }
}
