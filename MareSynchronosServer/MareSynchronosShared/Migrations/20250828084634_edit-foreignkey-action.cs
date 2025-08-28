using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MareSynchronosServer.Migrations
{
    /// <inheritdoc />
    public partial class editforeignkeyaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_pfinder_users_user_id",
                table: "pfinder");

            migrationBuilder.AddForeignKey(
                name: "fk_pfinder_users_user_id",
                table: "pfinder",
                column: "user_id",
                principalTable: "users",
                principalColumn: "uid",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_pfinder_users_user_id",
                table: "pfinder");

            migrationBuilder.AddForeignKey(
                name: "fk_pfinder_users_user_id",
                table: "pfinder",
                column: "user_id",
                principalTable: "users",
                principalColumn: "uid");
        }
    }
}
