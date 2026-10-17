using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Rosters.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialRosters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rosters");

            migrationBuilder.CreateTable(
                name: "tournament_teams",
                schema: "rosters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "player_registrations",
                schema: "rosters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SquadNumber = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    JoinedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    LeftOn = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_registrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_player_registrations_tournament_teams_TournamentTeamId",
                        column: x => x.TournamentTeamId,
                        principalSchema: "rosters",
                        principalTable: "tournament_teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_registrations_PlayerId",
                schema: "rosters",
                table: "player_registrations",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_player_registrations_TournamentTeamId",
                schema: "rosters",
                table: "player_registrations",
                column: "TournamentTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_teams_TournamentId_TeamId",
                schema: "rosters",
                table: "tournament_teams",
                columns: new[] { "TournamentId", "TeamId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_registrations",
                schema: "rosters");

            migrationBuilder.DropTable(
                name: "tournament_teams",
                schema: "rosters");
        }
    }
}
