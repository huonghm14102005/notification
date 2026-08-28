using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDevicePushEndpoints : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "device_push_endpoints",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                device_id = table.Column<Guid>(type: "uuid", nullable: false),
                platform = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                token_encrypted = table.Column<byte[]>(type: "bytea", nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                disabled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_device_push_endpoints", x => x.id);
                table.CheckConstraint("ck_device_push_endpoints_platform", "platform IN ('fcm', 'apns')");
                table.CheckConstraint("ck_device_push_endpoints_status", "status IN ('active', 'disabled')");
                table.ForeignKey(
                    name: "FK_device_push_endpoints_devices_device_id",
                    column: x => x.device_id,
                    principalTable: "devices",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_device_push_endpoints_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_device_push_endpoints_device_id",
            table: "device_push_endpoints",
            column: "device_id");

        migrationBuilder.CreateIndex(
            name: "ix_device_push_endpoints_tenant_status",
            table: "device_push_endpoints",
            columns: new[] { "tenant_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ux_device_push_endpoints_tenant_device",
            table: "device_push_endpoints",
            columns: new[] { "tenant_id", "device_id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "device_push_endpoints");
    }
}
