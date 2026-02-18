using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoria.Migrations
{
    /// <inheritdoc />
    public partial class RefactorCalendarEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlarmsJson",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "Classification",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "ExceptionDates",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "RecurrenceRuleJson",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CalendarEvents");

            migrationBuilder.RenameColumn(
                name: "TimeZoneId",
                table: "CalendarEvents",
                newName: "RecurrenceUntil");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "CalendarEvents",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceCount",
                table: "CalendarEvents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceFrequency",
                table: "CalendarEvents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceInterval",
                table: "CalendarEvents",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecurrenceCount",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "RecurrenceFrequency",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "RecurrenceInterval",
                table: "CalendarEvents");

            migrationBuilder.RenameColumn(
                name: "RecurrenceUntil",
                table: "CalendarEvents",
                newName: "TimeZoneId");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "CalendarEvents",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "AlarmsJson",
                table: "CalendarEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Classification",
                table: "CalendarEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExceptionDates",
                table: "CalendarEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecurrenceRuleJson",
                table: "CalendarEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "CalendarEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
