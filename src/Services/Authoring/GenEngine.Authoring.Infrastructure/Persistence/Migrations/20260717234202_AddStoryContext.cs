using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenEngine.Authoring.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoryContext : Migration
    {
        private static readonly string[] StoryContextIndexColumns = ["FrontId", "CategoryId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "scenarios",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreationBrief",
                table: "scenarios",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FrontId",
                table: "scenarios",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_scenarios_FrontId_CategoryId",
                table: "scenarios",
                columns: StoryContextIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_scenarios_FrontId_CategoryId",
                table: "scenarios");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "scenarios");

            migrationBuilder.DropColumn(
                name: "CreationBrief",
                table: "scenarios");

            migrationBuilder.DropColumn(
                name: "FrontId",
                table: "scenarios");
        }
    }
}