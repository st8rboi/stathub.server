using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stathub.Modules.Finance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

            migrationBuilder.CreateTable(
                name: "fee_settings",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fee_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "match_charges",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Side = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_charges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fee_settings_ScopeType_ScopeId",
                schema: "finance",
                table: "fee_settings",
                columns: new[] { "ScopeType", "ScopeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_match_charges_MatchId_Side",
                schema: "finance",
                table: "match_charges",
                columns: new[] { "MatchId", "Side" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_match_charges_StageId",
                schema: "finance",
                table: "match_charges",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_match_charges_TournamentId",
                schema: "finance",
                table: "match_charges",
                column: "TournamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fee_settings",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "match_charges",
                schema: "finance");
        }
    }
}
