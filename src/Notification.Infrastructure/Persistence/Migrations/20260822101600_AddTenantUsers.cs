using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTenantUsers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_admins_role",
            table: "admins");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "disabled_at",
            table: "admins",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "display_name",
            table: "admins",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "status",
            table: "admins",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "active");

        migrationBuilder.Sql("UPDATE admins SET display_name = split_part(email, '@', 1) WHERE display_name = '';");

        migrationBuilder.AddCheckConstraint(
            name: "ck_admins_disabled",
            table: "admins",
            sql: "(status = 'active' AND disabled_at IS NULL) OR (status = 'disabled' AND disabled_at IS NOT NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_admins_role",
            table: "admins",
            sql: "role IN ('owner','member')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_admins_status",
            table: "admins",
            sql: "status IN ('active','disabled')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM admins WHERE role = 'member') THEN RAISE EXCEPTION 'Cannot roll back AddTenantUsers while member accounts exist'; END IF; END $$;");
        migrationBuilder.DropCheckConstraint(
            name: "ck_admins_disabled",
            table: "admins");

        migrationBuilder.DropCheckConstraint(
            name: "ck_admins_role",
            table: "admins");

        migrationBuilder.DropCheckConstraint(
            name: "ck_admins_status",
            table: "admins");

        migrationBuilder.DropColumn(
            name: "disabled_at",
            table: "admins");

        migrationBuilder.DropColumn(
            name: "display_name",
            table: "admins");

        migrationBuilder.DropColumn(
            name: "status",
            table: "admins");

        migrationBuilder.AddCheckConstraint(
            name: "ck_admins_role",
            table: "admins",
            sql: "role IN ('owner')");
    }
}
