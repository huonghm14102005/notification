using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Devices;

namespace Notification.Infrastructure.Persistence.Configurations;

public sealed class DevicePushEndpointConfiguration : IEntityTypeConfiguration<DevicePushEndpoint>
{
    public void Configure(EntityTypeBuilder<DevicePushEndpoint> b)
    {
        b.ToTable("device_push_endpoints", t =>
        {
            t.HasCheckConstraint("ck_device_push_endpoints_platform", "platform IN ('fcm', 'apns')");
            t.HasCheckConstraint("ck_device_push_endpoints_status", "status IN ('active', 'disabled')");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.TenantId).HasColumnName("tenant_id");
        b.Property(x => x.DeviceId).HasColumnName("device_id");
        b.Property(x => x.Platform).HasColumnName("platform").HasMaxLength(16);
        b.Property(x => x.TokenEncrypted).HasColumnName("token_encrypted");
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(16);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.DisabledAt).HasColumnName("disabled_at");
        b.Property(x => x.LastDeliveredAt).HasColumnName("last_delivered_at");

        b.HasIndex(x => new { x.TenantId, x.DeviceId }).IsUnique().HasDatabaseName("ux_device_push_endpoints_tenant_device");
        b.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("ix_device_push_endpoints_tenant_status");

        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
    }
}
