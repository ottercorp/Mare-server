using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MareSynchronosServer.Migrations
{
    /// <inheritdoc />
    public partial class NewMoodlesStruct2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dispelable",
                table: "moodles");

            migrationBuilder.DropColumn(
                name: "stack_on_reapply",
                table: "moodles");

            migrationBuilder.DropColumn(
                name: "stacks_inc_on_reapply",
                table: "moodles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "dispelable",
                table: "moodles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "stack_on_reapply",
                table: "moodles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "stacks_inc_on_reapply",
                table: "moodles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
