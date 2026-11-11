using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Matches.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MatchTeamIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_matches_AwayTeamId",
                schema: "matches",
                table: "matches",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_matches_HomeTeamId",
                schema: "matches",
                table: "matches",
                column: "HomeTeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_matches_AwayTeamId",
                schema: "matches",
                table: "matches");

            migrationBuilder.DropIndex(
                name: "IX_matches_HomeTeamId",
                schema: "matches",
                table: "matches");
        }
    }
}
