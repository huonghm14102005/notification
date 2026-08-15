using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Identity;

namespace Notification.Infrastructure.Persistence.Configurations;

public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys", table =>
        {
            table.HasCheckConstraint("ck_api_keys_status", "status IN ('active','revoked')");
            table.HasCheckConstraint("ck_api_keys_revocation", "(status = 'active' AND revoked_at IS NULL) OR (status = 'revoked' AND revoked_at IS NOT NULL AND revoked_at >= created_at)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.CreatedByAdminId).HasColumnName("created_by_admin_id");
        builder.Property(x => x.ProducerName).HasColumnName("producer_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.KeyPrefix).HasColumnName("key_prefix").HasMaxLength(19).IsRequired();
        builder.Property(x => x.KeyHash).HasColumnName("key_hash").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
        builder.Property(x => x.LastUsedAt).HasColumnName("last_used_at"); builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.HasIndex(x => x.KeyPrefix).IsUnique().HasDatabaseName("ux_api_keys_prefix");
        builder.HasIndex(x => x.KeyHash).IsUnique().HasDatabaseName("ux_api_keys_hash");
        builder.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("ix_api_keys_tenant_status");
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt }).IsDescending(false, true).HasDatabaseName("ix_api_keys_tenant_created");
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByAdmin).WithMany().HasForeignKey(x => x.CreatedByAdminId).OnDelete(DeleteBehavior.Restrict);
    }
}
