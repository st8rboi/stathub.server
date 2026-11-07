using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Matches.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OfflineLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EventsForReview",
                schema: "matches",
                table: "matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinishedAtUtc",
                schema: "matches",
                table: "matches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PausedAtUtc",
                schema: "matches",
                table: "matches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PeriodPausedMilliseconds",
                schema: "matches",
                table: "matches",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurredAtUtc",
                schema: "matches",
                table: "match_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                schema: "matches",
                table: "match_events",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventsForReview",
                schema: "matches",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "FinishedAtUtc",
                schema: "matches",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "PausedAtUtc",
                schema: "matches",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "PeriodPausedMilliseconds",
                schema: "matches",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "OccurredAtUtc",
                schema: "matches",
                table: "match_events");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                schema: "matches",
                table: "match_events");
        }
    }
}
