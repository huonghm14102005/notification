using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRefreshTokens : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "refresh_tokens",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                family_id = table.Column<Guid>(type: "uuid", nullable: false),
                token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                replaced_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_refresh_tokens", x => x.id);
                table.CheckConstraint("ck_refresh_tokens_expiry", "expires_at > created_at");
                table.CheckConstraint("ck_refresh_tokens_revocation", "revoked_at IS NULL OR revoked_at >= created_at");
                table.ForeignKey(
                    name: "FK_refresh_tokens_admins_admin_id",
                    column: x => x.admin_id,
                    principalTable: "admins",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_refresh_tokens_refresh_tokens_replaced_by_id",
                    column: x => x.replaced_by_id,
                    principalTable: "refresh_tokens",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_refresh_tokens_admin_family",
            table: "refresh_tokens",
            columns: new[] { "admin_id", "family_id" });

        migrationBuilder.CreateIndex(
            name: "ix_refresh_tokens_expires_at_active",
            table: "refresh_tokens",
            column: "expires_at",
            filter: "revoked_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_refresh_tokens_replaced_by_id",
            table: "refresh_tokens",
            column: "replaced_by_id");

        migrationBuilder.CreateIndex(
            name: "ux_refresh_tokens_token_hash",
            table: "refresh_tokens",
            column: "token_hash",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "refresh_tokens");
    }
}
