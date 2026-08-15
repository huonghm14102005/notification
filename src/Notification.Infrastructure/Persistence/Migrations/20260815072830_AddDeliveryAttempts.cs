using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDeliveryAttempts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "delivery_attempts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                attempt_no = table.Column<int>(type: "integer", nullable: false),
                result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                provider_message_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_delivery_attempts", x => x.id);
                table.CheckConstraint("ck_delivery_attempts_no", "attempt_no >= 1");
                table.CheckConstraint("ck_delivery_attempts_outcome", "(result = 'success' AND error_code IS NULL) OR (result <> 'success' AND error_code IS NOT NULL)");
                table.CheckConstraint("ck_delivery_attempts_result", "result IN ('success','transient_failure','permanent_failure')");
                table.CheckConstraint("ck_delivery_attempts_time", "finished_at >= started_at");
                table.ForeignKey(
                    name: "FK_delivery_attempts_notifications_notification_id",
                    column: x => x.notification_id,
                    principalTable: "notifications",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_delivery_attempts_senders_sender_id",
                    column: x => x.sender_id,
                    principalTable: "senders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_delivery_attempts_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_delivery_attempts_sender_id",
            table: "delivery_attempts",
            column: "sender_id");

        migrationBuilder.CreateIndex(
            name: "ix_delivery_attempts_tenant_created",
            table: "delivery_attempts",
            columns: new[] { "tenant_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_delivery_attempts_tenant_notification",
            table: "delivery_attempts",
            columns: new[] { "tenant_id", "notification_id" });

        migrationBuilder.CreateIndex(
            name: "ux_delivery_attempts_notification_no",
            table: "delivery_attempts",
            columns: new[] { "notification_id", "attempt_no" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "delivery_attempts");
    }
}
