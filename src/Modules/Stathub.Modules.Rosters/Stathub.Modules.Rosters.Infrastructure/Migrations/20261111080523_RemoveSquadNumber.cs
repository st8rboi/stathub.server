using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Rosters.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSquadNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SquadNumber",
                schema: "rosters",
                table: "player_registrations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SquadNumber",
                schema: "rosters",
                table: "player_registrations",
                type: "integer",
                nullable: true);
        }
    }
}
