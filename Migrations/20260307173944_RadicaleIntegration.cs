using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoria.Migrations
{
    /// <inheritdoc />
    public partial class RadicaleIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Posts_CalendarEvents_CalendarEventId",
                table: "Posts");

            migrationBuilder.DropTable(
                name: "CalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_Posts_CalendarEventId",
                table: "Posts");

            migrationBuilder.CreateTable(
                name: "CalendarEventCache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SpaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccessPolicy = table.Column<int>(type: "INTEGER", nullable: false),
                    ETag = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEventCache", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEventCache_Spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "Spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalendarEventCache_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ContactCache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SpaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccessPolicy = table.Column<int>(type: "INTEGER", nullable: false),
                    ETag = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactCache", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactCache_Spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "Spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContactCache_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventCache_OwnerUserId",
                table: "CalendarEventCache",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventCache_SpaceId",
                table: "CalendarEventCache",
                column: "SpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactCache_OwnerUserId",
                table: "ContactCache",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactCache_SpaceId",
                table: "ContactCache",
                column: "SpaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarEventCache");

            migrationBuilder.DropTable(
                name: "ContactCache");

            migrationBuilder.CreateTable(
                name: "CalendarEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccessPolicy = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsAllDay = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecurrenceCount = table.Column<int>(type: "INTEGER", nullable: true),
                    RecurrenceFrequency = table.Column<int>(type: "INTEGER", nullable: true),
                    RecurrenceInterval = table.Column<int>(type: "INTEGER", nullable: true),
                    RecurrenceUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    SpaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_Spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "Spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_CalendarEventId",
                table: "Posts",
                column: "CalendarEventId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_EndDate",
                table: "CalendarEvents",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_OwnerUserId",
                table: "CalendarEvents",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_SpaceId_StartDate",
                table: "CalendarEvents",
                columns: new[] { "SpaceId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_StartDate",
                table: "CalendarEvents",
                column: "StartDate");

            migrationBuilder.AddForeignKey(
                name: "FK_Posts_CalendarEvents_CalendarEventId",
                table: "Posts",
                column: "CalendarEventId",
                principalTable: "CalendarEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
