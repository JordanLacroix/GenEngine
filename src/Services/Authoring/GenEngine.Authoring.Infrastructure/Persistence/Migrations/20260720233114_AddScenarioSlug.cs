using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenEngine.Authoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScenarioSlug : Migration
    {
        private static readonly string[] ScenarioSlugColumns = ["FrontId", "Slug"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "scenarios",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_scenarios_FrontId_Slug",
                table: "scenarios",
                columns: ScenarioSlugColumns,
                unique: true,
                filter: "\"Slug\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_scenarios_FrontId_Slug",
                table: "scenarios");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "scenarios");
        }
    }
}