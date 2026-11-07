using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Rosters.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StageScopedRosters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tournament_teams_TournamentId_TeamId",
                schema: "rosters",
                table: "tournament_teams");

            migrationBuilder.AddColumn<Guid>(
                name: "StageId",
                schema: "rosters",
                table: "tournament_teams",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_tournament_teams_StageId_TeamId",
                schema: "rosters",
                table: "tournament_teams",
                columns: new[] { "StageId", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tournament_teams_TournamentId",
                schema: "rosters",
                table: "tournament_teams",
                column: "TournamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tournament_teams_StageId_TeamId",
                schema: "rosters",
                table: "tournament_teams");

            migrationBuilder.DropIndex(
                name: "IX_tournament_teams_TournamentId",
                schema: "rosters",
                table: "tournament_teams");

            migrationBuilder.DropColumn(
                name: "StageId",
                schema: "rosters",
                table: "tournament_teams");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_teams_TournamentId_TeamId",
                schema: "rosters",
                table: "tournament_teams",
                columns: new[] { "TournamentId", "TeamId" },
                unique: true);
        }
    }
}
