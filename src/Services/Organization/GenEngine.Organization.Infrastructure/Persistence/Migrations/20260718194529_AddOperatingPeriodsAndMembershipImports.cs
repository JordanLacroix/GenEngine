using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace GenEngine.Organization.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatingPeriodsAndMembershipImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_memberships_FrontId_UserId_UnitId",
                table: "memberships");

            migrationBuilder.AddColumn<Guid>(
                name: "PeriodId",
                table: "memberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "operating_periods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FrontId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operating_periods", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_memberships_FrontId_PeriodId",
                table: "memberships",
                columns: new[] { "FrontId", "PeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_memberships_FrontId_UserId_UnitId_StartsAt",
                table: "memberships",
                columns: new[] { "FrontId", "UserId", "UnitId", "StartsAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_memberships_PeriodId",
                table: "memberships",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_operating_periods_FrontId_Code",
                table: "operating_periods",
                columns: new[] { "FrontId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operating_periods_FrontId_StartsAt_EndsAt",
                table: "operating_periods",
                columns: new[] { "FrontId", "StartsAt", "EndsAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_memberships_operating_periods_PeriodId",
                table: "memberships",
                column: "PeriodId",
                principalTable: "operating_periods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_memberships_operating_periods_PeriodId",
                table: "memberships");

            migrationBuilder.DropTable(
                name: "operating_periods");

            migrationBuilder.DropIndex(
                name: "IX_memberships_FrontId_PeriodId",
                table: "memberships");

            migrationBuilder.DropIndex(
                name: "IX_memberships_FrontId_UserId_UnitId_StartsAt",
                table: "memberships");

            migrationBuilder.DropIndex(
                name: "IX_memberships_PeriodId",
                table: "memberships");

            migrationBuilder.DropColumn(
                name: "PeriodId",
                table: "memberships");

            migrationBuilder.CreateIndex(
                name: "IX_memberships_FrontId_UserId_UnitId",
                table: "memberships",
                columns: new[] { "FrontId", "UserId", "UnitId" },
                unique: true);
        }
    }
}
#pragma warning restore CA1861