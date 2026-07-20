using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenEngine.PlayerExperience.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerStatValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_stat_values",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false),
                    ProcessedCommandIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_stat_values", x => new { x.ProfileId, x.Key });
                    table.ForeignKey(
                        name: "FK_player_stat_values_player_profiles_ProfileId",
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
                name: "player_stat_values");
        }
    }
}