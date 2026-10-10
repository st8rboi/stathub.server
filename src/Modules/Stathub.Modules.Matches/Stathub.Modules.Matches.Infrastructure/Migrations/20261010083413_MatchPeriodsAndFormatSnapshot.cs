using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Matches.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MatchPeriodsAndFormatSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "StartedAtUtc",
                schema: "matches",
                table: "matches",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<int>(
                name: "CurrentPeriod",
                schema: "matches",
                table: "matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ExtraTimeEnabled",
                schema: "matches",
                table: "matches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PenaltyShootoutEnabled",
                schema: "matches",
                table: "matches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PeriodDurationMinutes",
                schema: "matches",
                table: "matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStartedAtUtc",
                schema: "matches",
                table: "matches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeriodsCount",
                schema: "matches",
                table: "matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_matches_StageId",
                schema: "matches",
                table: "matches",
                column: "StageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_matches_StageId",
                schema: "matches",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "CurrentPeriod",
                schema: "matches",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "ExtraTimeEnabled",
                schema: "matches",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "PenaltyShootoutEnabled",
                schema: "matches",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "PeriodDurationMinutes",
                schema: "matches",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "PeriodStartedAtUtc",
                schema: "matches",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "PeriodsCount",
                schema: "matches",
                table: "matches");

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartedAtUtc",
                schema: "matches",
                table: "matches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
