using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Leagues.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagueDiscoveryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "leagues",
                table: "leagues",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DataSource",
                schema: "leagues",
                table: "leagues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                schema: "leagues",
                table: "leagues",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_leagues_City",
                schema: "leagues",
                table: "leagues",
                column: "City");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_leagues_City",
                schema: "leagues",
                table: "leagues");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "leagues",
                table: "leagues");

            migrationBuilder.DropColumn(
                name: "DataSource",
                schema: "leagues",
                table: "leagues");

            migrationBuilder.DropColumn(
                name: "Region",
                schema: "leagues",
                table: "leagues");
        }
    }
}
