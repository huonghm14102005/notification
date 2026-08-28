using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddScopedTemplateVersions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_templates_tenant_key",
            table: "templates");

        migrationBuilder.DropCheckConstraint(
            name: "ck_templates_body_length",
            table: "templates");

        migrationBuilder.RenameColumn(
            name: "key",
            table: "templates",
            newName: "template_code");

        migrationBuilder.RenameColumn(
            name: "body",
            table: "templates",
            newName: "text_body");

        migrationBuilder.AddColumn<string>(
            name: "audience",
            table: "templates",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "user");

        migrationBuilder.AddColumn<string>(
            name: "html_body",
            table: "templates",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "published_at",
            table: "templates",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "retired_at",
            table: "templates",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "scope",
            table: "templates",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "tenant");

        migrationBuilder.AddColumn<Guid>(
            name: "source_device_id",
            table: "templates",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "version",
            table: "templates",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.CreateIndex(
            name: "ix_templates_family_version",
            table: "templates",
            columns: new[] { "tenant_id", "template_code", "scope", "source_device_id", "version" });

        migrationBuilder.Sql("DROP INDEX ix_templates_family_version;");
        migrationBuilder.Sql("CREATE UNIQUE INDEX ux_templates_family_version ON templates (tenant_id, scope, COALESCE(source_device_id, '00000000-0000-0000-0000-000000000000'::uuid), template_code, version);");
        migrationBuilder.Sql("CREATE UNIQUE INDEX ux_templates_family_draft ON templates (tenant_id, scope, COALESCE(source_device_id, '00000000-0000-0000-0000-000000000000'::uuid), template_code) WHERE status = 'draft';");
        migrationBuilder.Sql("CREATE UNIQUE INDEX ux_templates_family_active ON templates (tenant_id, scope, COALESCE(source_device_id, '00000000-0000-0000-0000-000000000000'::uuid), template_code) WHERE status = 'active';");
        migrationBuilder.Sql("UPDATE templates SET published_at = updated_at WHERE status = 'active'; UPDATE templates SET retired_at = updated_at WHERE status = 'retired';");

        migrationBuilder.CreateIndex(
            name: "IX_templates_source_device_id",
            table: "templates",
            column: "source_device_id");

        migrationBuilder.AddCheckConstraint(
            name: "ck_templates_audience",
            table: "templates",
            sql: "audience IN ('user','system')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_templates_body",
            table: "templates",
            sql: "(text_body IS NOT NULL AND char_length(text_body) BETWEEN 1 AND 100000) OR (html_body IS NOT NULL AND char_length(html_body) BETWEEN 1 AND 100000)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_templates_scope",
            table: "templates",
            sql: "(scope = 'tenant' AND source_device_id IS NULL) OR (scope = 'source' AND source_device_id IS NOT NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_templates_version",
            table: "templates",
            sql: "version > 0");

        migrationBuilder.AddForeignKey(
            name: "FK_templates_devices_source_device_id",
            table: "templates",
            column: "source_device_id",
            principalTable: "devices",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_templates_devices_source_device_id",
            table: "templates");

        migrationBuilder.DropIndex(
            name: "ux_templates_family_version",
            table: "templates");

        migrationBuilder.DropIndex(name: "ux_templates_family_draft", table: "templates");
        migrationBuilder.DropIndex(name: "ux_templates_family_active", table: "templates");

        migrationBuilder.DropIndex(
            name: "IX_templates_source_device_id",
            table: "templates");

        migrationBuilder.DropCheckConstraint(
            name: "ck_templates_audience",
            table: "templates");

        migrationBuilder.DropCheckConstraint(
            name: "ck_templates_body",
            table: "templates");

        migrationBuilder.DropCheckConstraint(
            name: "ck_templates_scope",
            table: "templates");

        migrationBuilder.DropCheckConstraint(
            name: "ck_templates_version",
            table: "templates");

        migrationBuilder.Sql("UPDATE templates SET text_body = COALESCE(text_body, html_body, 'Unavailable');");

        migrationBuilder.AlterColumn<string>(
            name: "text_body",
            table: "templates",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.Sql("""
            WITH ranked AS (
                SELECT id,
                       FIRST_VALUE(id) OVER (
                           PARTITION BY tenant_id, template_code
                           ORDER BY CASE status WHEN 'active' THEN 0 WHEN 'draft' THEN 1 ELSE 2 END,
                                    version DESC, created_at DESC, id DESC) AS keep_id,
                       ROW_NUMBER() OVER (
                           PARTITION BY tenant_id, template_code
                           ORDER BY CASE status WHEN 'active' THEN 0 WHEN 'draft' THEN 1 ELSE 2 END,
                                    version DESC, created_at DESC, id DESC) AS row_no
                FROM templates
            )
            UPDATE notifications n
            SET template_id = r.keep_id
            FROM ranked r
            WHERE r.row_no > 1 AND n.template_id = r.id;

            WITH ranked AS (
                SELECT id,
                       ROW_NUMBER() OVER (
                           PARTITION BY tenant_id, template_code
                           ORDER BY CASE status WHEN 'active' THEN 0 WHEN 'draft' THEN 1 ELSE 2 END,
                                    version DESC, created_at DESC, id DESC) AS row_no
                FROM templates
            )
            DELETE FROM templates t USING ranked r WHERE t.id = r.id AND r.row_no > 1;
            """);

        migrationBuilder.DropColumn(
            name: "audience",
            table: "templates");

        migrationBuilder.DropColumn(
            name: "html_body",
            table: "templates");

        migrationBuilder.DropColumn(
            name: "published_at",
            table: "templates");

        migrationBuilder.DropColumn(
            name: "retired_at",
            table: "templates");

        migrationBuilder.DropColumn(
            name: "scope",
            table: "templates");

        migrationBuilder.DropColumn(
            name: "source_device_id",
            table: "templates");

        migrationBuilder.DropColumn(
            name: "version",
            table: "templates");

        migrationBuilder.RenameColumn(
            name: "template_code",
            table: "templates",
            newName: "key");

        migrationBuilder.RenameColumn(
            name: "text_body",
            table: "templates",
            newName: "body");

        migrationBuilder.CreateIndex(
            name: "ux_templates_tenant_key",
            table: "templates",
            columns: new[] { "tenant_id", "key" },
            unique: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_templates_body_length",
            table: "templates",
            sql: "char_length(body) BETWEEN 1 AND 100000");
    }
}
