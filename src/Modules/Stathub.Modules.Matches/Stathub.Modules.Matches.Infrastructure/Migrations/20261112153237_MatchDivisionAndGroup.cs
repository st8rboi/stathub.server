using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Matches.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MatchDivisionAndGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DivisionId",
                schema: "matches",
                table: "matches",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                schema: "matches",
                table: "matches",
                type: "uuid",
                nullable: true);

            // Перенос данных: миграция Leagues превращает каждый существующий этап в дивизион с тем же Id,
            // поэтому дивизион старого матча совпадает с его этапом.
            migrationBuilder.Sql("""UPDATE matches.matches SET "DivisionId" = "StageId";""");

            migrationBuilder.CreateIndex(
                name: "IX_matches_DivisionId",
                schema: "matches",
                table: "matches",
                column: "DivisionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_matches_DivisionId",
                schema: "matches",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "DivisionId",
                schema: "matches",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "GroupId",
                schema: "matches",
                table: "matches");
        }
    }
}
