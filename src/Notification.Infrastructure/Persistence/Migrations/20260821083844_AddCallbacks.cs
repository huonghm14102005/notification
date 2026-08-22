using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCallbacks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "callback_configured_at",
            table: "devices",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<byte[]>(
            name: "callback_secret_encrypted",
            table: "devices",
            type: "bytea",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "callback_url",
            table: "devices",
            type: "character varying(2048)",
            maxLength: 2048,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "status_events",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                public_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                device_id = table.Column<Guid>(type: "uuid", nullable: false),
                notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                payload_encrypted = table.Column<byte[]>(type: "bytea", nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                attempt_count = table.Column<int>(type: "integer", nullable: false),
                next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_status_events", x => x.id);
                table.CheckConstraint("ck_status_events_attempt_count", "attempt_count BETWEEN 0 AND 6");
                table.CheckConstraint("ck_status_events_status", "status IN ('pending','sending','delivered','failed','cancelled')");
                table.ForeignKey(
                    name: "FK_status_events_devices_device_id",
                    column: x => x.device_id,
                    principalTable: "devices",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_status_events_notifications_notification_id",
                    column: x => x.notification_id,
                    principalTable: "notifications",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_status_events_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "callback_attempts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                attempt_no = table.Column<int>(type: "integer", nullable: false),
                result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                http_status_code = table.Column<int>(type: "integer", nullable: true),
                error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_callback_attempts", x => x.id);
                table.CheckConstraint("ck_callback_attempts_error", "(result = 'success' AND error_code IS NULL) OR (result <> 'success' AND error_code IS NOT NULL)");
                table.CheckConstraint("ck_callback_attempts_no", "attempt_no BETWEEN 1 AND 6");
                table.CheckConstraint("ck_callback_attempts_result", "result IN ('success','transient_failure','permanent_failure')");
                table.CheckConstraint("ck_callback_attempts_time", "finished_at >= started_at");
                table.ForeignKey(
                    name: "FK_callback_attempts_status_events_event_id",
                    column: x => x.event_id,
                    principalTable: "status_events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_callback_attempts_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddCheckConstraint(
            name: "ck_devices_callback",
            table: "devices",
            sql: "(callback_url IS NULL AND callback_secret_encrypted IS NULL AND callback_configured_at IS NULL) OR (callback_url IS NOT NULL AND callback_secret_encrypted IS NOT NULL AND callback_configured_at IS NOT NULL)");

        migrationBuilder.CreateIndex(
            name: "IX_callback_attempts_tenant_id",
            table: "callback_attempts",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ux_callback_attempts_event_no",
            table: "callback_attempts",
            columns: new[] { "event_id", "attempt_no" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_status_events_device_id",
            table: "status_events",
            column: "device_id");

        migrationBuilder.CreateIndex(
            name: "ix_status_events_status_due",
            table: "status_events",
            columns: new[] { "status", "next_attempt_at" });

        migrationBuilder.CreateIndex(
            name: "IX_status_events_tenant_id",
            table: "status_events",
            column: "tenant_id");

        migrationBuilder.CreateIndex(
            name: "ux_status_events_notification_type",
            table: "status_events",
            columns: new[] { "notification_id", "event_type" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_status_events_public_id",
            table: "status_events",
            column: "public_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "callback_attempts");

        migrationBuilder.DropTable(
            name: "status_events");

        migrationBuilder.DropCheckConstraint(
            name: "ck_devices_callback",
            table: "devices");

        migrationBuilder.DropColumn(
            name: "callback_configured_at",
            table: "devices");

        migrationBuilder.DropColumn(
            name: "callback_secret_encrypted",
            table: "devices");

        migrationBuilder.DropColumn(
            name: "callback_url",
            table: "devices");
    }
}
