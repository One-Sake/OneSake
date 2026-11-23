using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneSake.Persistence.Migrations
{
    // Written by hand (no dotnet SDK in the environment where it was created).
    // Check with: dotnet ef migrations has-pending-model-changes --project OneSake.Persistence --startup-project OneSake.Server
    /// <inheritdoc />
    [DbContext(typeof(OneSakeDbContext))]
    [Migration("20261123033000_AddAccounts")]
    public partial class AddAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NormalizedUsername = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecoveryCodeHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SecurityStamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PlayerId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    FailedLoginCount = table.Column<int>(type: "integer", nullable: false),
                    LockoutEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_NormalizedUsername",
                table: "Accounts",
                column: "NormalizedUsername",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_PlayerId",
                table: "Accounts",
                column: "PlayerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Accounts");
        }
    }
}
