using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDevicesAndLinkApiKeys : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "device_id",
            table: "api_keys",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "devices",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                owner_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                normalized_legacy_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                disabled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_devices", x => x.id);
                table.CheckConstraint("ck_devices_disabled", "(status = 'active' AND disabled_at IS NULL) OR (status = 'disabled' AND disabled_at IS NOT NULL)");
                table.CheckConstraint("ck_devices_role", "role IN ('source','both')");
                table.CheckConstraint("ck_devices_status", "status IN ('active','disabled')");
                table.ForeignKey(
                    name: "FK_devices_admins_owner_admin_id",
                    column: x => x.owner_admin_id,
                    principalTable: "admins",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_devices_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.Sql("""
            INSERT INTO devices
                (id, tenant_id, owner_admin_id, name, normalized_legacy_name, role, status, created_at, updated_at, disabled_at)
            SELECT gen_random_uuid(), tenant_id, created_by_admin_id, producer_name,
                   lower(trim(producer_name)), 'source', 'active', created_at, created_at, NULL
            FROM (
                SELECT DISTINCT ON (tenant_id, lower(trim(producer_name)))
                       tenant_id, created_by_admin_id, producer_name, created_at, id
                FROM api_keys
                ORDER BY tenant_id, lower(trim(producer_name)), created_at, id
            ) first_keys;

            UPDATE api_keys AS key
            SET device_id = device.id
            FROM devices AS device
            WHERE device.tenant_id = key.tenant_id
              AND device.normalized_legacy_name = lower(trim(key.producer_name));
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "device_id",
            table: "api_keys",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_api_keys_device_status_created",
            table: "api_keys",
            columns: new[] { "device_id", "status", "created_at" },
            descending: new[] { false, false, true });

        migrationBuilder.CreateIndex(
            name: "ix_api_keys_tenant_device",
            table: "api_keys",
            columns: new[] { "tenant_id", "device_id" });

        migrationBuilder.CreateIndex(
            name: "IX_devices_owner_admin_id",
            table: "devices",
            column: "owner_admin_id");

        migrationBuilder.CreateIndex(
            name: "ix_devices_tenant_owner_created",
            table: "devices",
            columns: new[] { "tenant_id", "owner_admin_id", "created_at" },
            descending: new[] { false, false, true });

        migrationBuilder.CreateIndex(
            name: "ix_devices_tenant_status_created",
            table: "devices",
            columns: new[] { "tenant_id", "status", "created_at" },
            descending: new[] { false, false, true });

        migrationBuilder.CreateIndex(
            name: "ux_devices_tenant_legacy_name",
            table: "devices",
            columns: new[] { "tenant_id", "normalized_legacy_name" },
            unique: true,
            filter: "normalized_legacy_name IS NOT NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_api_keys_devices_device_id",
            table: "api_keys",
            column: "device_id",
            principalTable: "devices",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_api_keys_devices_device_id",
            table: "api_keys");

        migrationBuilder.DropTable(
            name: "devices");

        migrationBuilder.DropIndex(
            name: "ix_api_keys_device_status_created",
            table: "api_keys");

        migrationBuilder.DropIndex(
            name: "ix_api_keys_tenant_device",
            table: "api_keys");

        migrationBuilder.DropColumn(
            name: "device_id",
            table: "api_keys");
    }
}
