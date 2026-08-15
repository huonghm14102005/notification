using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTemplates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "templates",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                subject = table.Column<string>(type: "character varying(998)", maxLength: 998, nullable: false),
                body = table.Column<string>(type: "text", nullable: false),
                variables = table.Column<string>(type: "jsonb", nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_templates", x => x.id);
                table.CheckConstraint("ck_templates_body_length", "char_length(body) BETWEEN 1 AND 100000");
                table.CheckConstraint("ck_templates_status", "status IN ('draft','active','retired')");
                table.CheckConstraint("ck_templates_variables_array", "jsonb_typeof(variables) = 'array'");
                table.ForeignKey(
                    name: "FK_templates_tenants_tenant_id",
                    column: x => x.tenant_id,
                    principalTable: "tenants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_templates_tenant_status_created",
            table: "templates",
            columns: new[] { "tenant_id", "status", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ux_templates_tenant_key",
            table: "templates",
            columns: new[] { "tenant_id", "key" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "templates");
    }
}
