using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Discipline.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialDiscipline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "discipline");

            migrationBuilder.CreateTable(
                name: "disqualifications",
                schema: "discipline",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MatchesBanned = table.Column<int>(type: "integer", nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LiftedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disqualifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_disqualifications_TournamentId_TeamId_PlayerId",
                schema: "discipline",
                table: "disqualifications",
                columns: new[] { "TournamentId", "TeamId", "PlayerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "disqualifications",
                schema: "discipline");
        }
    }
}
