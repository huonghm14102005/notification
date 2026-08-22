using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddFailureAlerts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "failure_alerts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                window_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                window_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                attempt_count = table.Column<int>(type: "integer", nullable: false),
                recipient_count = table.Column<int>(type: "integer", nullable: false),
                success_count = table.Column<int>(type: "integer", nullable: false),
                failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_failure_alerts", x => x.id);
                table.CheckConstraint("ck_failure_alerts_status", "status IN ('pending','sending','delivered','partially_delivered','failed')");
                table.ForeignKey(
                    name: "FK_failure_alerts_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "failure_incidents",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                window_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                window_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                component = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                sample_message = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                occurrence_count = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_failure_incidents", x => x.id);
                table.CheckConstraint("ck_failure_incidents_count", "occurrence_count > 0");
                table.ForeignKey(
                    name: "FK_failure_incidents_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_failure_alerts_due",
            table: "failure_alerts",
            columns: new[] { "status", "window_end", "created_at", "id" });

        migrationBuilder.CreateIndex(
            name: "ix_failure_alerts_tenant_created",
            table: "failure_alerts",
            columns: new[] { "tenant_id", "created_at", "id" });

        migrationBuilder.CreateIndex(
            name: "ux_failure_alerts_tenant_window",
            table: "failure_alerts",
            columns: new[] { "tenant_id", "window_start" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_failure_incidents_tenant_window",
            table: "failure_incidents",
            columns: new[] { "tenant_id", "window_end", "id" });

        migrationBuilder.CreateIndex(
            name: "ux_failure_incidents_fingerprint",
            table: "failure_incidents",
            columns: new[] { "tenant_id", "window_start", "component", "channel", "error_code" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "failure_alerts");

        migrationBuilder.DropTable(
            name: "failure_incidents");
    }
}
