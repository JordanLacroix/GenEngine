using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenEngine.Play.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFrontContextToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FrontId",
                table: "game_sessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "default");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FrontId",
                table: "game_sessions");
        }
    }
}