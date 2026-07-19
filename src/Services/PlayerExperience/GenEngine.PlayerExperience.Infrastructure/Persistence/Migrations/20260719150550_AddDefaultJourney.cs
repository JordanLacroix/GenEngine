using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenEngine.PlayerExperience.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultJourney : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultJourneyId",
                table: "player_profiles",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultJourneyId",
                table: "player_profiles");
        }
    }
}