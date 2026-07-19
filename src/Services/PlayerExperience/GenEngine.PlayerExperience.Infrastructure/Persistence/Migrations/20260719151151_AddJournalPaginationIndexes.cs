using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenEngine.PlayerExperience.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalPaginationIndexes : Migration
    {
        private static readonly string[] JournalCategoryColumns = ["ProfileId", "CategoryId", "OccurredAt"];
        private static readonly string[] JournalJourneyColumns = ["ProfileId", "JourneyId", "OccurredAt"];
        private static readonly string[] JournalScenarioColumns = ["ProfileId", "ScenarioId", "OccurredAt"];
        private static readonly string[] JournalTypeColumns = ["ProfileId", "Type", "OccurredAt"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_player_journal_entries_ProfileId_CategoryId_OccurredAt",
                table: "player_journal_entries",
                columns: JournalCategoryColumns);

            migrationBuilder.CreateIndex(
                name: "IX_player_journal_entries_ProfileId_JourneyId_OccurredAt",
                table: "player_journal_entries",
                columns: JournalJourneyColumns);

            migrationBuilder.CreateIndex(
                name: "IX_player_journal_entries_ProfileId_ScenarioId_OccurredAt",
                table: "player_journal_entries",
                columns: JournalScenarioColumns);

            migrationBuilder.CreateIndex(
                name: "IX_player_journal_entries_ProfileId_Type_OccurredAt",
                table: "player_journal_entries",
                columns: JournalTypeColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_player_journal_entries_ProfileId_CategoryId_OccurredAt",
                table: "player_journal_entries");

            migrationBuilder.DropIndex(
                name: "IX_player_journal_entries_ProfileId_JourneyId_OccurredAt",
                table: "player_journal_entries");

            migrationBuilder.DropIndex(
                name: "IX_player_journal_entries_ProfileId_ScenarioId_OccurredAt",
                table: "player_journal_entries");

            migrationBuilder.DropIndex(
                name: "IX_player_journal_entries_ProfileId_Type_OccurredAt",
                table: "player_journal_entries");
        }
    }
}