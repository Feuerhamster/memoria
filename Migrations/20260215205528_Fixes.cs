using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoria.Migrations
{
    /// <inheritdoc />
    public partial class Fixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileMetadataPost_Post_PostId",
                table: "FileMetadataPost");

            migrationBuilder.DropForeignKey(
                name: "FK_Post_Post_ParentId",
                table: "Post");

            migrationBuilder.DropForeignKey(
                name: "FK_Post_Post_RootParentId",
                table: "Post");

            migrationBuilder.DropForeignKey(
                name: "FK_Post_Users_CreatorUserId",
                table: "Post");

            migrationBuilder.DropForeignKey(
                name: "FK_TextNote_Post_PostId",
                table: "TextNote");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TextNote",
                table: "TextNote");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Post",
                table: "Post");

            migrationBuilder.RenameTable(
                name: "TextNote",
                newName: "TextNotes");

            migrationBuilder.RenameTable(
                name: "Post",
                newName: "Posts");

            migrationBuilder.RenameIndex(
                name: "IX_TextNote_PostId",
                table: "TextNotes",
                newName: "IX_TextNotes_PostId");

            migrationBuilder.RenameIndex(
                name: "IX_Post_RootParentId",
                table: "Posts",
                newName: "IX_Posts_RootParentId");

            migrationBuilder.RenameIndex(
                name: "IX_Post_ParentId",
                table: "Posts",
                newName: "IX_Posts_ParentId");

            migrationBuilder.RenameIndex(
                name: "IX_Post_CreatorUserId",
                table: "Posts",
                newName: "IX_Posts_CreatorUserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TextNotes",
                table: "TextNotes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Posts",
                table: "Posts",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FileMetadataPost_Posts_PostId",
                table: "FileMetadataPost",
                column: "PostId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_Posts_ParentId",
                table: "Posts",
                column: "ParentId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_Posts_RootParentId",
                table: "Posts",
                column: "RootParentId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_Users_CreatorUserId",
                table: "Posts",
                column: "CreatorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TextNotes_Posts_PostId",
                table: "TextNotes",
                column: "PostId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileMetadataPost_Posts_PostId",
                table: "FileMetadataPost");

            migrationBuilder.DropForeignKey(
                name: "FK_Posts_Posts_ParentId",
                table: "Posts");

            migrationBuilder.DropForeignKey(
                name: "FK_Posts_Posts_RootParentId",
                table: "Posts");

            migrationBuilder.DropForeignKey(
                name: "FK_Posts_Users_CreatorUserId",
                table: "Posts");

            migrationBuilder.DropForeignKey(
                name: "FK_TextNotes_Posts_PostId",
                table: "TextNotes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TextNotes",
                table: "TextNotes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Posts",
                table: "Posts");

            migrationBuilder.RenameTable(
                name: "TextNotes",
                newName: "TextNote");

            migrationBuilder.RenameTable(
                name: "Posts",
                newName: "Post");

            migrationBuilder.RenameIndex(
                name: "IX_TextNotes_PostId",
                table: "TextNote",
                newName: "IX_TextNote_PostId");

            migrationBuilder.RenameIndex(
                name: "IX_Posts_RootParentId",
                table: "Post",
                newName: "IX_Post_RootParentId");

            migrationBuilder.RenameIndex(
                name: "IX_Posts_ParentId",
                table: "Post",
                newName: "IX_Post_ParentId");

            migrationBuilder.RenameIndex(
                name: "IX_Posts_CreatorUserId",
                table: "Post",
                newName: "IX_Post_CreatorUserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TextNote",
                table: "TextNote",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Post",
                table: "Post",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FileMetadataPost_Post_PostId",
                table: "FileMetadataPost",
                column: "PostId",
                principalTable: "Post",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Post_Post_ParentId",
                table: "Post",
                column: "ParentId",
                principalTable: "Post",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Post_Post_RootParentId",
                table: "Post",
                column: "RootParentId",
                principalTable: "Post",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Post_Users_CreatorUserId",
                table: "Post",
                column: "CreatorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TextNote_Post_PostId",
                table: "TextNote",
                column: "PostId",
                principalTable: "Post",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
