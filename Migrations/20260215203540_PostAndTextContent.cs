using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoria.Migrations
{
    /// <inheritdoc />
    public partial class PostAndTextContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Post",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatorUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SpaceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RootParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Post", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Post_Post_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Post",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Post_Post_RootParentId",
                        column: x => x.RootParentId,
                        principalTable: "Post",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Post_Users_CreatorUserId",
                        column: x => x.CreatorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FileMetadataPost",
                columns: table => new
                {
                    FilesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PostId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileMetadataPost", x => new { x.FilesId, x.PostId });
                    table.ForeignKey(
                        name: "FK_FileMetadataPost_Files_FilesId",
                        column: x => x.FilesId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FileMetadataPost_Post_PostId",
                        column: x => x.PostId,
                        principalTable: "Post",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TextNote",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    PostId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsInSpaceDocs = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextNote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TextNote_Post_PostId",
                        column: x => x.PostId,
                        principalTable: "Post",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Spaces_ImageId",
                table: "Spaces",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_SpaceId",
                table: "Files",
                column: "SpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_FileMetadataPost_PostId",
                table: "FileMetadataPost",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Post_CreatorUserId",
                table: "Post",
                column: "CreatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Post_ParentId",
                table: "Post",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Post_RootParentId",
                table: "Post",
                column: "RootParentId");

            migrationBuilder.CreateIndex(
                name: "IX_TextNote_PostId",
                table: "TextNote",
                column: "PostId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Spaces_SpaceId",
                table: "Files",
                column: "SpaceId",
                principalTable: "Spaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Spaces_Files_ImageId",
                table: "Spaces",
                column: "ImageId",
                principalTable: "Files",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_Spaces_SpaceId",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_Spaces_Files_ImageId",
                table: "Spaces");

            migrationBuilder.DropTable(
                name: "FileMetadataPost");

            migrationBuilder.DropTable(
                name: "TextNote");

            migrationBuilder.DropTable(
                name: "Post");

            migrationBuilder.DropIndex(
                name: "IX_Spaces_ImageId",
                table: "Spaces");

            migrationBuilder.DropIndex(
                name: "IX_Files_SpaceId",
                table: "Files");
        }
    }
}
