using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateChannelAndDeviceRoleConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_devices_role",
                table: "devices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_deliveries_channel",
                table: "deliveries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_devices_role",
                table: "devices",
                sql: "role IN ('source','both','recipient')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_deliveries_channel",
                table: "deliveries",
                sql: "channel IN ('email','telegram','discord','push')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_devices_role",
                table: "devices");

            migrationBuilder.DropCheckConstraint(
                name: "ck_deliveries_channel",
                table: "deliveries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_devices_role",
                table: "devices",
                sql: "role IN ('source','both')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_deliveries_channel",
                table: "deliveries",
                sql: "channel = 'email'");
        }
    }
}
