using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenEngine.Authoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaginationIndexes : Migration
    {
        private static readonly string[] ScenarioArchiveColumns = ["IsArchived", "CategoryId"];
        private static readonly string[] ScenarioOwnerTimelineColumns = ["OwnerId", "UpdatedAt"];
        private static readonly string[] VersionTimelineColumns = ["ScenarioId", "PublishedAt"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_scenarios_IsArchived_CategoryId",
                table: "scenarios",
                columns: ScenarioArchiveColumns);

            migrationBuilder.CreateIndex(
                name: "IX_scenarios_OwnerId_UpdatedAt",
                table: "scenarios",
                columns: ScenarioOwnerTimelineColumns);

            migrationBuilder.CreateIndex(
                name: "IX_scenario_versions_ScenarioId_PublishedAt",
                table: "scenario_versions",
                columns: VersionTimelineColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_scenarios_IsArchived_CategoryId",
                table: "scenarios");

            migrationBuilder.DropIndex(
                name: "IX_scenarios_OwnerId_UpdatedAt",
                table: "scenarios");

            migrationBuilder.DropIndex(
                name: "IX_scenario_versions_ScenarioId_PublishedAt",
                table: "scenario_versions");
        }
    }
}