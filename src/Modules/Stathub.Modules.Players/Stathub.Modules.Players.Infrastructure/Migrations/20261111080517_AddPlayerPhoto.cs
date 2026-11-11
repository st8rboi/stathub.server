using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Players.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerPhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PhotoOwnerTenantId",
                schema: "players",
                table: "players",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                schema: "players",
                table: "players",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoOwnerTenantId",
                schema: "players",
                table: "players");

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                schema: "players",
                table: "players");
        }
    }
}
