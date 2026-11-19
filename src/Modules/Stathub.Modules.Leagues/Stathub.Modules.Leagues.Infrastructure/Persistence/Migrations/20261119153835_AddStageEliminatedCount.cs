using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Leagues.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStageEliminatedCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EliminatedCount",
                schema: "leagues",
                table: "stages",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EliminatedCount",
                schema: "leagues",
                table: "stages");
        }
    }
}
