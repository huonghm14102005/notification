using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Senders;

namespace Notification.Infrastructure.Persistence.Configurations;

public sealed class SenderConfiguration : IEntityTypeConfiguration<Sender>
{
    public void Configure(EntityTypeBuilder<Sender> b)
    {
        b.ToTable("senders", t => { t.HasCheckConstraint("ck_senders_channel", "channel = 'email'"); t.HasCheckConstraint("ck_senders_port", "port BETWEEN 1 AND 65535"); t.HasCheckConstraint("ck_senders_status", "status IN ('active','disabled')"); t.HasCheckConstraint("ck_senders_disabled_default", "status <> 'disabled' OR is_default = false"); });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.Key).HasColumnName("key").HasMaxLength(63); b.Property(x => x.Channel).HasColumnName("channel").HasMaxLength(16);
        b.Property(x => x.Host).HasColumnName("host").HasMaxLength(253); b.Property(x => x.Port).HasColumnName("port"); b.Property(x => x.Secure).HasColumnName("secure");
        b.Property(x => x.Username).HasColumnName("username").HasMaxLength(254); b.Property(x => x.PasswordEncrypted).HasColumnName("password_encrypted");
        b.Property(x => x.FromEmail).HasColumnName("from_email").HasMaxLength(254); b.Property(x => x.FromName).HasColumnName("from_name").HasMaxLength(200);
        b.Property(x => x.IsDefault).HasColumnName("is_default"); b.Property(x => x.Status).HasColumnName("status").HasMaxLength(16);
        b.Property(x => x.VerifiedAt).HasColumnName("verified_at"); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => new { x.TenantId, x.Key }).IsUnique().HasDatabaseName("ux_senders_tenant_key");
        b.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("ix_senders_tenant_status");
        b.HasIndex(x => x.TenantId).IsUnique().HasFilter("is_default = true AND status = 'active'").HasDatabaseName("ux_senders_tenant_default");
        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
