using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDeliveries : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_delivery_attempts_notifications_notification_id",
            table: "delivery_attempts");

        migrationBuilder.DropForeignKey(
            name: "FK_notifications_senders_sender_id",
            table: "notifications");

        migrationBuilder.DropIndex(
            name: "IX_notifications_sender_id",
            table: "notifications");

        migrationBuilder.DropIndex(
            name: "ix_notifications_status_next_attempt",
            table: "notifications");

        migrationBuilder.DropCheckConstraint(
            name: "ck_notifications_attempt_count",
            table: "notifications");

        migrationBuilder.DropCheckConstraint(
            name: "ck_notifications_status",
            table: "notifications");

        migrationBuilder.DropColumn(
            name: "attempt_count",
            table: "notifications");

        migrationBuilder.DropColumn(
            name: "next_attempt_at",
            table: "notifications");

        migrationBuilder.DropColumn(
            name: "recipient_email",
            table: "notifications");

        migrationBuilder.DropColumn(
            name: "recipient_ref",
            table: "notifications");

        migrationBuilder.DropColumn(
            name: "sender_id",
            table: "notifications");

        migrationBuilder.RenameColumn(
            name: "sent_at",
            table: "notifications",
            newName: "completed_at");

        migrationBuilder.RenameColumn(
            name: "notification_id",
            table: "delivery_attempts",
            newName: "delivery_id");

        migrationBuilder.RenameIndex(
            name: "ux_delivery_attempts_notification_no",
            table: "delivery_attempts",
            newName: "ux_delivery_attempts_delivery_no");

        migrationBuilder.RenameIndex(
            name: "ix_delivery_attempts_tenant_notification",
            table: "delivery_attempts",
            newName: "ix_delivery_attempts_tenant_delivery");

        migrationBuilder.CreateTable(
            name: "deliveries",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                sender_id = table.Column<Guid>(type: "uuid", nullable: true),
                channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                target = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                target_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                attempt_count = table.Column<int>(type: "integer", nullable: false),
                next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_deliveries", x => x.id);
                table.CheckConstraint("ck_deliveries_attempt_count", "attempt_count BETWEEN 0 AND 4");
                table.CheckConstraint("ck_deliveries_channel", "channel = 'email'");
                table.CheckConstraint("ck_deliveries_status", "status IN ('pending','sending','delivered','failed','cancelled')");
                table.ForeignKey(
                    name: "FK_deliveries_notifications_notification_id",
                    column: x => x.notification_id,
                    principalTable: "notifications",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_deliveries_senders_sender_id",
                    column: x => x.sender_id,
                    principalTable: "senders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_deliveries_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddCheckConstraint(
            name: "ck_notifications_status",
            table: "notifications",
            sql: "status IN ('accepted','processing','delivered','partially_delivered','failed','cancelled')");

        migrationBuilder.CreateIndex(
            name: "IX_deliveries_sender_id",
            table: "deliveries",
            column: "sender_id");

        migrationBuilder.CreateIndex(
            name: "ix_deliveries_status_due",
            table: "deliveries",
            columns: new[] { "status", "next_attempt_at", "created_at", "id" });

        migrationBuilder.CreateIndex(
            name: "ix_deliveries_tenant_notification",
            table: "deliveries",
            columns: new[] { "tenant_id", "notification_id" });

        migrationBuilder.CreateIndex(
            name: "ux_deliveries_notification_channel_target",
            table: "deliveries",
            columns: new[] { "notification_id", "channel", "target" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_delivery_attempts_deliveries_delivery_id",
            table: "delivery_attempts",
            column: "delivery_id",
            principalTable: "deliveries",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_delivery_attempts_deliveries_delivery_id",
            table: "delivery_attempts");

        migrationBuilder.DropCheckConstraint(
            name: "ck_notifications_status",
            table: "notifications");

        migrationBuilder.RenameColumn(
            name: "completed_at",
            table: "notifications",
            newName: "sent_at");

        migrationBuilder.RenameColumn(
            name: "delivery_id",
            table: "delivery_attempts",
            newName: "notification_id");

        migrationBuilder.RenameIndex(
            name: "ux_delivery_attempts_delivery_no",
            table: "delivery_attempts",
            newName: "ux_delivery_attempts_notification_no");

        migrationBuilder.RenameIndex(
            name: "ix_delivery_attempts_tenant_delivery",
            table: "delivery_attempts",
            newName: "ix_delivery_attempts_tenant_notification");

        migrationBuilder.AddColumn<int>(
            name: "attempt_count",
            table: "notifications",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "next_attempt_at",
            table: "notifications",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "recipient_email",
            table: "notifications",
            type: "character varying(254)",
            maxLength: 254,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "recipient_ref",
            table: "notifications",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "sender_id",
            table: "notifications",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.Sql(@"UPDATE notifications n SET
                sender_id = d.sender_id,
                recipient_email = d.target,
                recipient_ref = d.target_ref,
                attempt_count = d.attempt_count,
                next_attempt_at = d.next_attempt_at,
                sent_at = d.delivered_at,
                status = CASE n.status WHEN 'processing' THEN 'sending' WHEN 'delivered' THEN 'sent'
                    WHEN 'partially_delivered' THEN 'failed' ELSE n.status END
                FROM deliveries d WHERE d.notification_id = n.id;
                UPDATE delivery_attempts a SET notification_id = d.notification_id
                FROM deliveries d WHERE a.notification_id = d.id;");

        migrationBuilder.DropTable(
            name: "deliveries");

        migrationBuilder.CreateIndex(
            name: "IX_notifications_sender_id",
            table: "notifications",
            column: "sender_id");

        migrationBuilder.CreateIndex(
            name: "ix_notifications_status_next_attempt",
            table: "notifications",
            columns: new[] { "status", "next_attempt_at" });

        migrationBuilder.AddCheckConstraint(
            name: "ck_notifications_attempt_count",
            table: "notifications",
            sql: "attempt_count >= 0");

        migrationBuilder.AddCheckConstraint(
            name: "ck_notifications_status",
            table: "notifications",
            sql: "status IN ('accepted','sending','sent','failed','cancelled')");

        migrationBuilder.AddForeignKey(
            name: "FK_delivery_attempts_notifications_notification_id",
            table: "delivery_attempts",
            column: "notification_id",
            principalTable: "notifications",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_notifications_senders_sender_id",
            table: "notifications",
            column: "sender_id",
            principalTable: "senders",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }
}
