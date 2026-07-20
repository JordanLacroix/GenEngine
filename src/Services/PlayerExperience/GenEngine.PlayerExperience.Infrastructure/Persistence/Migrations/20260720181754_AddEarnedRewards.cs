using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenEngine.PlayerExperience.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEarnedRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_earned_rewards",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    RewardId = table.Column<Guid>(type: "uuid", nullable: false),
                    EarnedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_earned_rewards", x => new { x.ProfileId, x.RewardId });
                    table.ForeignKey(
                        name: "FK_player_earned_rewards_player_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "player_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_earned_rewards");
        }
    }
}