using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notification.Domain.Identity;

namespace Notification.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", table =>
        {
            table.HasCheckConstraint("ck_refresh_tokens_expiry", "expires_at > created_at");
            table.HasCheckConstraint("ck_refresh_tokens_revocation", "revoked_at IS NULL OR revoked_at >= created_at");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.AdminId).HasColumnName("admin_id");
        builder.Property(x => x.FamilyId).HasColumnName("family_id");
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.ReplacedById).HasColumnName("replaced_by_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("ux_refresh_tokens_token_hash");
        builder.HasIndex(x => new { x.AdminId, x.FamilyId }).HasDatabaseName("ix_refresh_tokens_admin_family");
        builder.HasIndex(x => x.ExpiresAt).HasFilter("revoked_at IS NULL").HasDatabaseName("ix_refresh_tokens_expires_at_active");
        builder.HasOne(x => x.Admin).WithMany().HasForeignKey(x => x.AdminId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RefreshToken>().WithMany().HasForeignKey(x => x.ReplacedById).OnDelete(DeleteBehavior.Restrict);
    }
}
