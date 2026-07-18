using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenEngine.PlayerExperience.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        private static readonly string[] ProfileIndexColumns = ["UserId", "FrontId"];
        private static readonly string[] WalletIndexColumns = ["ProfileId", "IdempotencyKey"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FrontId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    FamiliarId = table.Column<Guid>(type: "uuid", nullable: true),
                    FamiliarForm = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    FamiliarTone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    FamiliarWritingStyle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FamiliarAccent = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    FamiliarHelpLevel = table.Column<int>(type: "integer", nullable: false),
                    Balance = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "owned_items",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcquiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_owned_items", x => new { x.ProfileId, x.OfferId });
                    table.ForeignKey(
                        name: "FK_owned_items_player_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "player_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wallet_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    BalanceAfter = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallet_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wallet_entries_player_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "player_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_profiles_UserId_FrontId",
                table: "player_profiles",
                columns: ProfileIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wallet_entries_ProfileId_IdempotencyKey",
                table: "wallet_entries",
                columns: WalletIndexColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "owned_items");

            migrationBuilder.DropTable(
                name: "wallet_entries");

            migrationBuilder.DropTable(
                name: "player_profiles");
        }
    }
}