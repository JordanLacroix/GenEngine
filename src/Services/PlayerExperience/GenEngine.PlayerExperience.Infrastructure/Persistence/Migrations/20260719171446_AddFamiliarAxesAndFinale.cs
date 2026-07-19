using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenEngine.PlayerExperience.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFamiliarAxesAndFinale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FamiliarAxisSelectionsJson",
                table: "player_profiles",
                type: "jsonb",
                nullable: false,
                // An empty string is not valid JSON: existing rows must be backfilled
                // with an empty object, otherwise the column cannot be read back.
                defaultValue: "{}");

            migrationBuilder.AddColumn<Guid>(
                name: "FinaleId",
                table: "player_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinaleReachedAt",
                table: "player_profiles",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FamiliarAxisSelectionsJson",
                table: "player_profiles");

            migrationBuilder.DropColumn(
                name: "FinaleId",
                table: "player_profiles");

            migrationBuilder.DropColumn(
                name: "FinaleReachedAt",
                table: "player_profiles");
        }
    }
}
