using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Templates;
namespace Notification.Infrastructure.Persistence.Configurations;

public sealed class ContentTemplateConfiguration : IEntityTypeConfiguration<ContentTemplate>
{
    public void Configure(EntityTypeBuilder<ContentTemplate> b)
    {
        b.ToTable("templates", t => { t.HasCheckConstraint("ck_templates_status", "status IN ('draft','active','retired')"); t.HasCheckConstraint("ck_templates_scope", "(scope = 'tenant' AND source_device_id IS NULL) OR (scope = 'source' AND source_device_id IS NOT NULL)"); t.HasCheckConstraint("ck_templates_audience", "audience IN ('user','system')"); t.HasCheckConstraint("ck_templates_version", "version > 0"); t.HasCheckConstraint("ck_templates_body", "(text_body IS NOT NULL AND char_length(text_body) BETWEEN 1 AND 100000) OR (html_body IS NOT NULL AND char_length(html_body) BETWEEN 1 AND 100000)"); t.HasCheckConstraint("ck_templates_variables_array", "jsonb_typeof(variables) = 'array'"); });
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.TenantId).HasColumnName("tenant_id"); b.Property(x => x.TemplateCode).HasColumnName("template_code").HasMaxLength(63); b.Ignore(x => x.Key); b.Property(x => x.Scope).HasColumnName("scope").HasMaxLength(16); b.Property(x => x.SourceDeviceId).HasColumnName("source_device_id"); b.Property(x => x.Audience).HasColumnName("audience").HasMaxLength(16); b.Property(x => x.Version).HasColumnName("version"); b.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(998); b.Property(x => x.TextBody).HasColumnName("text_body"); b.Property(x => x.HtmlBody).HasColumnName("html_body"); b.Ignore(x => x.Body);
        b.Property(x => x.Variables).HasColumnName("variables").HasColumnType("jsonb").HasConversion(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<string>(), new ValueComparer<string[]>((a, c) => a!.SequenceEqual(c!), v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x.GetHashCode())), v => v.ToArray()));
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(16); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.UpdatedAt).HasColumnName("updated_at"); b.Property(x => x.PublishedAt).HasColumnName("published_at"); b.Property(x => x.RetiredAt).HasColumnName("retired_at");
        b.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt }).HasDatabaseName("ix_templates_tenant_status_created"); b.HasIndex(x => new { x.TenantId, x.TemplateCode, x.Scope, x.SourceDeviceId, x.Version }).HasDatabaseName("ix_templates_family_version"); b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.SourceDevice).WithMany().HasForeignKey(x => x.SourceDeviceId).OnDelete(DeleteBehavior.Restrict);
    }
}
