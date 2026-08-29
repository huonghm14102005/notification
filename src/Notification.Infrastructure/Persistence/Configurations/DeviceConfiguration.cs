using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Devices;

namespace Notification.Infrastructure.Persistence.Configurations;

public sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices", table =>
        {
            table.HasCheckConstraint("ck_devices_role", "role IN ('source','both','recipient')");
            table.HasCheckConstraint("ck_devices_status", "status IN ('active','disabled')");
            table.HasCheckConstraint("ck_devices_disabled", "(status = 'active' AND disabled_at IS NULL) OR (status = 'disabled' AND disabled_at IS NOT NULL)");
            table.HasCheckConstraint("ck_devices_callback", "(callback_url IS NULL AND callback_secret_encrypted IS NULL AND callback_configured_at IS NULL) OR (callback_url IS NOT NULL AND callback_secret_encrypted IS NOT NULL AND callback_configured_at IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.TenantId).HasColumnName("tenant_id");
        builder.Property(x => x.OwnerAdminId).HasColumnName("owner_admin_id"); builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.NormalizedLegacyName).HasColumnName("normalized_legacy_name").HasMaxLength(100);
        builder.Property(x => x.Role).HasColumnName("role").HasMaxLength(16).IsRequired(); builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(16).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at"); builder.Property(x => x.UpdatedAt).HasColumnName("updated_at"); builder.Property(x => x.DisabledAt).HasColumnName("disabled_at");
        builder.Property(x => x.CallbackUrl).HasColumnName("callback_url").HasMaxLength(2048);
        builder.Property(x => x.CallbackSecretEncrypted).HasColumnName("callback_secret_encrypted");
        builder.Property(x => x.CallbackConfiguredAt).HasColumnName("callback_configured_at");
        builder.HasIndex(x => new { x.TenantId, x.NormalizedLegacyName }).IsUnique().HasFilter("normalized_legacy_name IS NOT NULL").HasDatabaseName("ux_devices_tenant_legacy_name");
        builder.HasIndex(x => new { x.TenantId, x.OwnerAdminId, x.CreatedAt }).IsDescending(false, false, true).HasDatabaseName("ix_devices_tenant_owner_created");
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt }).IsDescending(false, false, true).HasDatabaseName("ix_devices_tenant_status_created");
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OwnerAdmin).WithMany().HasForeignKey(x => x.OwnerAdminId).OnDelete(DeleteBehavior.Restrict);
    }
}
