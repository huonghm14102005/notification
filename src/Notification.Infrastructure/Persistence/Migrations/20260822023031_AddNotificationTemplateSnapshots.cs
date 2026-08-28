using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddNotificationTemplateSnapshots : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_notifications_ciphertext",
            table: "notifications");

        migrationBuilder.RenameColumn(
            name: "body_encrypted",
            table: "notifications",
            newName: "text_body_encrypted");

        migrationBuilder.AlterColumn<byte[]>(
            name: "text_body_encrypted",
            table: "notifications",
            type: "bytea",
            nullable: true,
            oldClrType: typeof(byte[]),
            oldType: "bytea");

        migrationBuilder.AddColumn<byte[]>(
            name: "html_body_encrypted",
            table: "notifications",
            type: "bytea",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_notifications_ciphertext",
            table: "notifications",
            sql: "octet_length(subject_encrypted) > 0 AND (octet_length(text_body_encrypted) > 0 OR octet_length(html_body_encrypted) > 0)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_notifications_ciphertext",
            table: "notifications");

        migrationBuilder.Sql("UPDATE notifications SET text_body_encrypted = html_body_encrypted WHERE text_body_encrypted IS NULL;");

        migrationBuilder.AlterColumn<byte[]>(
            name: "text_body_encrypted",
            table: "notifications",
            type: "bytea",
            nullable: false,
            oldClrType: typeof(byte[]),
            oldType: "bytea",
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "html_body_encrypted",
            table: "notifications");

        migrationBuilder.RenameColumn(
            name: "text_body_encrypted",
            table: "notifications",
            newName: "body_encrypted");

        migrationBuilder.AddCheckConstraint(
            name: "ck_notifications_ciphertext",
            table: "notifications",
            sql: "octet_length(subject_encrypted) > 0 AND octet_length(body_encrypted) > 0");
    }
}
