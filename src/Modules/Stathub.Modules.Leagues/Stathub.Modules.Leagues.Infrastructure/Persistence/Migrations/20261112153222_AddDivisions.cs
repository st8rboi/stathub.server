using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Leagues.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDivisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DivisionId",
                schema: "leagues",
                table: "stages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "QualifiersCount",
                schema: "leagues",
                table: "stages",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "divisions",
                schema: "leagues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_divisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_divisions_tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalSchema: "leagues",
                        principalTable: "tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stage_groups",
                schema: "leagues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TeamIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stage_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stage_groups_stages_StageId",
                        column: x => x.StageId,
                        principalSchema: "leagues",
                        principalTable: "stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stages_DivisionId",
                schema: "leagues",
                table: "stages",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_divisions_TournamentId",
                schema: "leagues",
                table: "divisions",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_stage_groups_StageId",
                schema: "leagues",
                table: "stage_groups",
                column: "StageId");

            // Перенос данных. Каждый существующий этап становится дивизионом с тем же Id: заявки команд
            // (rosters.tournament_teams) и матчи ссылались на этап, и после переноса те же идентификаторы
            // указывают на дивизион — другим модулям достаточно переименовать/скопировать колонку.
            // Если этапов в турнире несколько, они были дивизионами: дивизион забирает имя этапа,
            // а этап становится его единственным «Основным этапом». Единственный этап остаётся как есть
            // внутри «Основного дивизиона».
            migrationBuilder.Sql("""
                INSERT INTO leagues.divisions ("Id", "TournamentId", "Order", "Name")
                SELECT s."Id",
                       s."TournamentId",
                       ROW_NUMBER() OVER (PARTITION BY s."TournamentId" ORDER BY s."Order", s."Id"),
                       CASE WHEN COUNT(*) OVER (PARTITION BY s."TournamentId") > 1
                            THEN s."Name" ELSE 'Основной дивизион' END
                FROM leagues.stages s;

                UPDATE leagues.stages s
                SET "DivisionId" = s."Id",
                    "Order" = 1,
                    "Name" = CASE WHEN c.cnt > 1 THEN 'Основной этап' ELSE s."Name" END
                FROM (SELECT "TournamentId", COUNT(*) AS cnt FROM leagues.stages GROUP BY "TournamentId") c
                WHERE c."TournamentId" = s."TournamentId";

                INSERT INTO leagues.divisions ("Id", "TournamentId", "Order", "Name")
                SELECT gen_random_uuid(), t."Id", 1, 'Основной дивизион'
                FROM leagues.tournaments t
                WHERE NOT EXISTS (SELECT 1 FROM leagues.divisions d WHERE d."TournamentId" = t."Id");
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_stages_divisions_DivisionId",
                schema: "leagues",
                table: "stages",
                column: "DivisionId",
                principalSchema: "leagues",
                principalTable: "divisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stages_divisions_DivisionId",
                schema: "leagues",
                table: "stages");

            migrationBuilder.DropTable(
                name: "divisions",
                schema: "leagues");

            migrationBuilder.DropTable(
                name: "stage_groups",
                schema: "leagues");

            migrationBuilder.DropIndex(
                name: "IX_stages_DivisionId",
                schema: "leagues",
                table: "stages");

            migrationBuilder.DropColumn(
                name: "DivisionId",
                schema: "leagues",
                table: "stages");

            migrationBuilder.DropColumn(
                name: "QualifiersCount",
                schema: "leagues",
                table: "stages");
        }
    }
}
