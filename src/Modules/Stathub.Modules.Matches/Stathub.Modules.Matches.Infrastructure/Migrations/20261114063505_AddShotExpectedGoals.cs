using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Matches.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShotExpectedGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedGoals",
                schema: "matches",
                table: "match_events",
                type: "numeric(4,2)",
                precision: 4,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedGoals",
                schema: "matches",
                table: "match_events");
        }
    }
}
