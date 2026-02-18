using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoria.Migrations
{
    /// <inheritdoc />
    public partial class RefactorCalendarEventStandalone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEvents_Posts_PostId",
                table: "CalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_PostId",
                table: "CalendarEvents");

            migrationBuilder.RenameColumn(
                name: "PostId",
                table: "CalendarEvents",
                newName: "SpaceId");

            migrationBuilder.AddColumn<Guid>(
                name: "CalendarEventId",
                table: "Posts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccessPolicy",
                table: "CalendarEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "CalendarEvents",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Posts_CalendarEventId",
                table: "Posts",
                column: "CalendarEventId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_OwnerUserId",
                table: "CalendarEvents",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_SpaceId_StartDate",
                table: "CalendarEvents",
                columns: new[] { "SpaceId", "StartDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEvents_Spaces_SpaceId",
                table: "CalendarEvents",
                column: "SpaceId",
                principalTable: "Spaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEvents_Users_OwnerUserId",
                table: "CalendarEvents",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_CalendarEvents_CalendarEventId",
                table: "Posts",
                column: "CalendarEventId",
                principalTable: "CalendarEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEvents_Spaces_SpaceId",
                table: "CalendarEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEvents_Users_OwnerUserId",
                table: "CalendarEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Posts_CalendarEvents_CalendarEventId",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Posts_CalendarEventId",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_OwnerUserId",
                table: "CalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_SpaceId_StartDate",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "CalendarEventId",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "AccessPolicy",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "CalendarEvents");

            migrationBuilder.RenameColumn(
                name: "SpaceId",
                table: "CalendarEvents",
                newName: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_PostId",
                table: "CalendarEvents",
                column: "PostId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEvents_Posts_PostId",
                table: "CalendarEvents",
                column: "PostId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
