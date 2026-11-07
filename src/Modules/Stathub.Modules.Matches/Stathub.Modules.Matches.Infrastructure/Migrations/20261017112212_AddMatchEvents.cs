using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Matches.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "match_events",
                schema: "matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedPlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ClientTimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_match_events_matches_MatchId",
                        column: x => x.MatchId,
                        principalSchema: "matches",
                        principalTable: "matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_match_events_MatchId_SequenceNumber",
                schema: "matches",
                table: "match_events",
                columns: new[] { "MatchId", "SequenceNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "match_events",
                schema: "matches");
        }
    }
}
