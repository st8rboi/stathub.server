using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Rosters.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TournamentTeamDivision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StageId",
                schema: "rosters",
                table: "tournament_teams",
                newName: "DivisionId");

            migrationBuilder.RenameIndex(
                name: "IX_tournament_teams_StageId_TeamId",
                schema: "rosters",
                table: "tournament_teams",
                newName: "IX_tournament_teams_DivisionId_TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DivisionId",
                schema: "rosters",
                table: "tournament_teams",
                newName: "StageId");

            migrationBuilder.RenameIndex(
                name: "IX_tournament_teams_DivisionId_TeamId",
                schema: "rosters",
                table: "tournament_teams",
                newName: "IX_tournament_teams_StageId_TeamId");
        }
    }
}
