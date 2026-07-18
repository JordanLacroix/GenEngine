using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenEngine.Configuration.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "experience_configurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FrontId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DocumentJson = table.Column<string>(type: "jsonb", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    PublishedVersion = table.Column<int>(type: "integer", nullable: false),
                    PublishedJson = table.Column<string>(type: "jsonb", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experience_configurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_experience_configurations_FrontId",
                table: "experience_configurations",
                column: "FrontId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "experience_configurations");
        }
    }
}