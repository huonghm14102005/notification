using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddNotificationIntake : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                api_key_id = table.Column<Guid>(type: "uuid", nullable: false),
                sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                template_id = table.Column<Guid>(type: "uuid", nullable: true),
                recipient_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                recipient_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                subject_encrypted = table.Column<byte[]>(type: "bytea", nullable: false),
                body_encrypted = table.Column<byte[]>(type: "bytea", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                attempt_count = table.Column<int>(type: "integer", nullable: false),
                next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notifications", x => x.id);
                table.CheckConstraint("ck_notifications_attempt_count", "attempt_count >= 0");
                table.CheckConstraint("ck_notifications_ciphertext", "octet_length(subject_encrypted) > 0 AND octet_length(body_encrypted) > 0");
                table.CheckConstraint("ck_notifications_status", "status IN ('accepted','sending','sent','failed','cancelled')");
                table.ForeignKey(
                    name: "FK_notifications_api_keys_api_key_id",
                    column: x => x.api_key_id,
                    principalTable: "api_keys",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_notifications_senders_sender_id",
                    column: x => x.sender_id,
                    principalTable: "senders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_notifications_templates_template_id",
                    column: x => x.template_id,
                    principalTable: "templates",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_notifications_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_notifications_api_key_id",
            table: "notifications",
            column: "api_key_id");

        migrationBuilder.CreateIndex(
            name: "IX_notifications_sender_id",
            table: "notifications",
            column: "sender_id");

        migrationBuilder.CreateIndex(
            name: "ix_notifications_status_next_attempt",
            table: "notifications",
            columns: new[] { "status", "next_attempt_at" });

        migrationBuilder.CreateIndex(
            name: "IX_notifications_template_id",
            table: "notifications",
            column: "template_id");

        migrationBuilder.CreateIndex(
            name: "ix_notifications_tenant_created",
            table: "notifications",
            columns: new[] { "tenant_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_notifications_tenant_status",
            table: "notifications",
            columns: new[] { "tenant_id", "status" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "notifications");
    }
}
