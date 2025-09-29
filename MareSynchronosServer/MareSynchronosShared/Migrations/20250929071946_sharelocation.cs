using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MareSynchronosServer.Migrations
{
    /// <inheritdoc />
    public partial class sharelocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "share_location",
                table: "user_permission_sets",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "share_location",
                table: "group_pair_preferred_permissions",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "share_location",
                table: "user_permission_sets");

            migrationBuilder.DropColumn(
                name: "share_location",
                table: "group_pair_preferred_permissions");
        }
    }
}
