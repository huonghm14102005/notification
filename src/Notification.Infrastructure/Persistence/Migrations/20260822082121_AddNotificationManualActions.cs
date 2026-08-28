using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddNotificationManualActions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "notification_manual_actions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                source_notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                result_notification_id = table.Column<Guid>(type: "uuid", nullable: true),
                action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notification_manual_actions", x => x.id);
                table.CheckConstraint("ck_notification_manual_actions_action", "action IN ('retry','cancel')");
                table.ForeignKey(
                    name: "FK_notification_manual_actions_admins_admin_id",
                    column: x => x.admin_id,
                    principalTable: "admins",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_notification_manual_actions_notifications_result_notificati~",
                    column: x => x.result_notification_id,
                    principalTable: "notifications",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_notification_manual_actions_notifications_source_notificati~",
                    column: x => x.source_notification_id,
                    principalTable: "notifications",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_notification_manual_actions_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_notification_manual_actions_admin_id",
            table: "notification_manual_actions",
            column: "admin_id");

        migrationBuilder.CreateIndex(
            name: "IX_notification_manual_actions_result_notification_id",
            table: "notification_manual_actions",
            column: "result_notification_id");

        migrationBuilder.CreateIndex(
            name: "IX_notification_manual_actions_source_notification_id",
            table: "notification_manual_actions",
            column: "source_notification_id");

        migrationBuilder.CreateIndex(
            name: "ix_notification_manual_actions_tenant_created",
            table: "notification_manual_actions",
            columns: new[] { "tenant_id", "created_at", "id" });

        migrationBuilder.CreateIndex(
            name: "ux_notification_manual_actions_source_action",
            table: "notification_manual_actions",
            columns: new[] { "tenant_id", "source_notification_id", "action" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "notification_manual_actions");
    }
}
