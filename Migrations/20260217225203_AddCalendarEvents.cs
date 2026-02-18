using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoria.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PostId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Location = table.Column<string>(type: "TEXT", nullable: true),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsAllDay = table.Column<bool>(type: "INTEGER", nullable: false),
                    TimeZoneId = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Classification = table.Column<int>(type: "INTEGER", nullable: false),
                    RecurrenceRuleJson = table.Column<string>(type: "TEXT", nullable: true),
                    ExceptionDates = table.Column<string>(type: "TEXT", nullable: true),
                    AlarmsJson = table.Column<string>(type: "TEXT", nullable: true),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_EndDate",
                table: "CalendarEvents",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_PostId",
                table: "CalendarEvents",
                column: "PostId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_StartDate",
                table: "CalendarEvents",
                column: "StartDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarEvents");
        }
    }
}
