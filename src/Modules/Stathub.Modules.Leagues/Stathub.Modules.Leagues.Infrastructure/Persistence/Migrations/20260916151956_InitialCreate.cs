using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Leagues.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "leagues");

            migrationBuilder.CreateTable(
                name: "leagues",
                schema: "leagues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Sport = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leagues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tournaments",
                schema: "leagues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournaments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stages",
                schema: "leagues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FormatType = table.Column<int>(type: "integer", nullable: false),
                    win_points = table.Column<int>(type: "integer", nullable: false),
                    draw_points = table.Column<int>(type: "integer", nullable: false),
                    loss_points = table.Column<int>(type: "integer", nullable: false),
                    period_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    periods_count = table.Column<int>(type: "integer", nullable: false),
                    extra_time_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    penalty_shootout_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stages_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalSchema: "leagues",
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stage_tiebreaker_rules",
                schema: "leagues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Criterion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stage_tiebreaker_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stage_tiebreaker_rules_stages_StageId",
                        column: x => x.StageId,
                        principalSchema: "leagues",
                        principalTable: "stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_leagues_Slug",
                schema: "leagues",
                table: "leagues",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stage_tiebreaker_rules_StageId",
                schema: "leagues",
                table: "stage_tiebreaker_rules",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_stages_TournamentId",
                schema: "leagues",
                table: "stages",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_tournaments_LeagueId",
                schema: "leagues",
                table: "tournaments",
                column: "LeagueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "leagues",
                schema: "leagues");

            migrationBuilder.DropTable(
                name: "stage_tiebreaker_rules",
                schema: "leagues");

            migrationBuilder.DropTable(
                name: "stages",
                schema: "leagues");

            migrationBuilder.DropTable(
                name: "tournaments",
                schema: "leagues");
        }
    }
}
