using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenEngine.Authoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        private static readonly string[] ScenarioVersionIndexColumns = ["ScenarioId", "Number"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scenarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DraftJson = table.Column<string>(type: "jsonb", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scenarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "scenario_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    SnapshotHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scenario_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scenario_versions_scenarios_ScenarioId",
                        column: x => x.ScenarioId,
                        principalTable: "scenarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scenario_versions_ScenarioId_Number",
                table: "scenario_versions",
                columns: ScenarioVersionIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scenario_versions_SnapshotHash",
                table: "scenario_versions",
                column: "SnapshotHash");

            migrationBuilder.CreateIndex(
                name: "IX_scenarios_OwnerId",
                table: "scenarios",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scenario_versions");

            migrationBuilder.DropTable(
                name: "scenarios");
        }
    }
}