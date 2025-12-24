using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MareSynchronosServer.Migrations
{
    /// <inheritdoc />
    public partial class NewMoodlesStruct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "status_on_dispell",
                table: "moodles",
                newName: "chained_status");

            migrationBuilder.AddColumn<int>(
                name: "chain_trigger",
                table: "moodles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "modifier",
                table: "moodles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "stack_steps",
                table: "moodles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "chain_trigger",
                table: "moodles");

            migrationBuilder.DropColumn(
                name: "modifier",
                table: "moodles");

            migrationBuilder.DropColumn(
                name: "stack_steps",
                table: "moodles");

            migrationBuilder.RenameColumn(
                name: "chained_status",
                table: "moodles",
                newName: "status_on_dispell");
        }
    }
}
