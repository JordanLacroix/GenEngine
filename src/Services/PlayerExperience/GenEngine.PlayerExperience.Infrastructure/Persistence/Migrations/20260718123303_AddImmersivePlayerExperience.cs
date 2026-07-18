using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenEngine.PlayerExperience.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImmersivePlayerExperience : Migration
    {
        private static readonly string[] JournalIdempotencyColumns = ["ProfileId", "IdempotencyKey"];
        private static readonly string[] JournalTimelineColumns = ["ProfileId", "OccurredAt"];
        private static readonly string[] MasteryScenarioColumns = ["ProfileId", "ScenarioId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FamiliarCustomName",
                table: "player_profiles",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "FamiliarInterventionFrequency",
                table: "player_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "FamiliarProactive",
                table: "player_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "onboarding_states",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    TutorialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CompletedStepIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ProcessedCommandIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SkippedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_states", x => new { x.ProfileId, x.TutorialId, x.Version });
                    table.ForeignKey(
                        name: "FK_onboarding_states_player_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "player_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "player_journal_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    JourneyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScenarioVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_journal_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_player_journal_entries_player_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "player_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scenario_masteries",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChoiceIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    NodeIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    EndingIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    SessionIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ProcessedCommandIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    TotalObjectives = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scenario_masteries", x => new { x.ProfileId, x.ScenarioVersionId });
                    table.ForeignKey(
                        name: "FK_scenario_masteries_player_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "player_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_journal_entries_ProfileId_IdempotencyKey",
                table: "player_journal_entries",
                columns: JournalIdempotencyColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_player_journal_entries_ProfileId_OccurredAt",
                table: "player_journal_entries",
                columns: JournalTimelineColumns);

            migrationBuilder.CreateIndex(
                name: "IX_scenario_masteries_ProfileId_ScenarioId",
                table: "scenario_masteries",
                columns: MasteryScenarioColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "onboarding_states");

            migrationBuilder.DropTable(
                name: "player_journal_entries");

            migrationBuilder.DropTable(
                name: "scenario_masteries");

            migrationBuilder.DropColumn(
                name: "FamiliarCustomName",
                table: "player_profiles");

            migrationBuilder.DropColumn(
                name: "FamiliarInterventionFrequency",
                table: "player_profiles");

            migrationBuilder.DropColumn(
                name: "FamiliarProactive",
                table: "player_profiles");
        }
    }
}
