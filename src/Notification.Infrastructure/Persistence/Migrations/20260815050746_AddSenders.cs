using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSenders : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "senders",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                host = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                port = table.Column<int>(type: "integer", nullable: false),
                secure = table.Column<bool>(type: "boolean", nullable: false),
                username = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                password_encrypted = table.Column<byte[]>(type: "bytea", nullable: false),
                from_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                from_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                is_default = table.Column<bool>(type: "boolean", nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_senders", x => x.id);
                table.CheckConstraint("ck_senders_channel", "channel = 'email'");
                table.CheckConstraint("ck_senders_disabled_default", "status <> 'disabled' OR is_default = false");
                table.CheckConstraint("ck_senders_port", "port BETWEEN 1 AND 65535");
                table.CheckConstraint("ck_senders_status", "status IN ('active','disabled')");
                table.ForeignKey(
                    name: "FK_senders_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_senders_tenant_status",
            table: "senders",
            columns: new[] { "tenant_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ux_senders_tenant_default",
            table: "senders",
            column: "tenant_id",
            unique: true,
            filter: "is_default = true AND status = 'active'");

        migrationBuilder.CreateIndex(
            name: "ux_senders_tenant_key",
            table: "senders",
            columns: new[] { "tenant_id", "key" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "senders");
    }
}
