using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneSake.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRankingAndTurnOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstPlayerGuestId",
                table: "MatchResults",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRanked",
                table: "MatchResults",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LoserRatingAfter",
                table: "MatchResults",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoserRatingBefore",
                table: "MatchResults",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WinnerRatingAfter",
                table: "MatchResults",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WinnerRatingBefore",
                table: "MatchResults",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WinnerWentFirst",
                table: "MatchResults",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlayerRatings",
                columns: table => new
                {
                    GuestId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Wins = table.Column<int>(type: "integer", nullable: false),
                    Losses = table.Column<int>(type: "integer", nullable: false),
                    Peak = table.Column<int>(type: "integer", nullable: false),
                    LastPlayedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FavoriteLeaderCardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerRatings", x => x.GuestId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_EndedAtUtc",
                table: "MatchResults",
                column: "EndedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRatings_Rating",
                table: "PlayerRatings",
                column: "Rating");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerRatings");

            migrationBuilder.DropIndex(
                name: "IX_MatchResults_EndedAtUtc",
                table: "MatchResults");

            migrationBuilder.DropColumn(
                name: "FirstPlayerGuestId",
                table: "MatchResults");

            migrationBuilder.DropColumn(
                name: "IsRanked",
                table: "MatchResults");

            migrationBuilder.DropColumn(
                name: "LoserRatingAfter",
                table: "MatchResults");

            migrationBuilder.DropColumn(
                name: "LoserRatingBefore",
                table: "MatchResults");

            migrationBuilder.DropColumn(
                name: "WinnerRatingAfter",
                table: "MatchResults");

            migrationBuilder.DropColumn(
                name: "WinnerRatingBefore",
                table: "MatchResults");

            migrationBuilder.DropColumn(
                name: "WinnerWentFirst",
                table: "MatchResults");
        }
    }
}
