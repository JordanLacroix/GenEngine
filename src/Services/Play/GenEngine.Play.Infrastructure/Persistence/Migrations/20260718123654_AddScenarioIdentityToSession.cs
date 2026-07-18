using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenEngine.Play.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScenarioIdentityToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ScenarioId",
                table: "game_sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_game_sessions_ScenarioId",
                table: "game_sessions",
                column: "ScenarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_game_sessions_ScenarioId",
                table: "game_sessions");

            migrationBuilder.DropColumn(
                name: "ScenarioId",
                table: "game_sessions");
        }
    }
}
