using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Templates;
namespace Notification.Infrastructure.Persistence.Configurations;

public sealed class ContentTemplateConfiguration : IEntityTypeConfiguration<ContentTemplate>
{
    public void Configure(EntityTypeBuilder<ContentTemplate> b) { b.ToTable("templates", t => { t.HasCheckConstraint("ck_templates_status", "status IN ('draft','active','retired')"); t.HasCheckConstraint("ck_templates_body_length", "char_length(body) BETWEEN 1 AND 100000"); t.HasCheckConstraint("ck_templates_variables_array", "jsonb_typeof(variables) = 'array'"); }); b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.TenantId).HasColumnName("tenant_id"); b.Property(x => x.Key).HasColumnName("key").HasMaxLength(63); b.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(998); b.Property(x => x.Body).HasColumnName("body"); b.Property(x => x.Variables).HasColumnName("variables").HasColumnType("jsonb").HasConversion(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<string>(), new ValueComparer<string[]>((a, c) => a!.SequenceEqual(c!), v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x.GetHashCode())), v => v.ToArray())); b.Property(x => x.Status).HasColumnName("status").HasMaxLength(16); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.UpdatedAt).HasColumnName("updated_at"); b.HasIndex(x => new { x.TenantId, x.Key }).IsUnique().HasDatabaseName("ux_templates_tenant_key"); b.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt }).HasDatabaseName("ix_templates_tenant_status_created"); b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict); }
}
