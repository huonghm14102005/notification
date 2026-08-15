using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddApiKeys : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "api_keys",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_by_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                producer_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                key_prefix = table.Column<string>(type: "character varying(19)", maxLength: 19, nullable: false),
                key_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_api_keys", x => x.id);
                table.CheckConstraint("ck_api_keys_revocation", "(status = 'active' AND revoked_at IS NULL) OR (status = 'revoked' AND revoked_at IS NOT NULL AND revoked_at >= created_at)");
                table.CheckConstraint("ck_api_keys_status", "status IN ('active','revoked')");
                table.ForeignKey(
                    name: "FK_api_keys_admins_created_by_admin_id",
                    column: x => x.created_by_admin_id,
                    principalTable: "admins",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_api_keys_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_api_keys_created_by_admin_id",
            table: "api_keys",
            column: "created_by_admin_id");

        migrationBuilder.CreateIndex(
            name: "ix_api_keys_tenant_created",
            table: "api_keys",
            columns: new[] { "tenant_id", "created_at" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "ix_api_keys_tenant_status",
            table: "api_keys",
            columns: new[] { "tenant_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ux_api_keys_hash",
            table: "api_keys",
            column: "key_hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_api_keys_prefix",
            table: "api_keys",
            column: "key_prefix",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "api_keys");
    }
}
