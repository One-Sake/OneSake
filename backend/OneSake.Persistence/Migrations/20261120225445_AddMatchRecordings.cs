using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OneSake.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchRecordings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BugReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecordingId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoomCode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    GuestId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CardId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AtSeq = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BugReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchRecordings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchResultId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoomCode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchRecordings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_CreatedAtUtc",
                table: "BugReports",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_GuestId",
                table: "BugReports",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_BugReports_RecordingId",
                table: "BugReports",
                column: "RecordingId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchRecordings_CreatedAtUtc",
                table: "MatchRecordings",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MatchRecordings_MatchResultId",
                table: "MatchRecordings",
                column: "MatchResultId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BugReports");

            migrationBuilder.DropTable(
                name: "MatchRecordings");
        }
    }
}
