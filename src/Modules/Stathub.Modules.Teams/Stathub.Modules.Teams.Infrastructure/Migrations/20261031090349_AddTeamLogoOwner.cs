using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Teams.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamLogoOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LogoOwnerTenantId",
                schema: "teams",
                table: "teams",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoOwnerTenantId",
                schema: "teams",
                table: "teams");
        }
    }
}
