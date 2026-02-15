using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoria.Migrations
{
    /// <inheritdoc />
    public partial class Files : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessPolicy",
                table: "Files",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Files_OwnerUserId",
                table: "Files",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Users_OwnerUserId",
                table: "Files",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_Users_OwnerUserId",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_Files_OwnerUserId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "AccessPolicy",
                table: "Files");
        }
    }
}
