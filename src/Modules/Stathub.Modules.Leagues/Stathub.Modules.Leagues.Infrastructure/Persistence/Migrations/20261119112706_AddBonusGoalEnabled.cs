using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Leagues.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBonusGoalEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "bonus_goal_enabled",
                schema: "leagues",
                table: "stages",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bonus_goal_enabled",
                schema: "leagues",
                table: "stages");
        }
    }
}
