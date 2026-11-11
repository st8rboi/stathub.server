using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Rosters.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TournamentTeamTeamIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_tournament_teams_TeamId",
                schema: "rosters",
                table: "tournament_teams",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tournament_teams_TeamId",
                schema: "rosters",
                table: "tournament_teams");
        }
    }
}
