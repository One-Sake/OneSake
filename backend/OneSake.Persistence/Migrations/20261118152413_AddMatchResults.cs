using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneSake.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomCode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TurnCount = table.Column<int>(type: "integer", nullable: false),
                    EndReason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WinnerGuestId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    WinnerDisplayName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WinnerLeaderCardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LoserGuestId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    LoserDisplayName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LoserLeaderCardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchResults", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_LoserGuestId",
                table: "MatchResults",
                column: "LoserGuestId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchResults_WinnerGuestId",
                table: "MatchResults",
                column: "WinnerGuestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchResults");
        }
    }
}
