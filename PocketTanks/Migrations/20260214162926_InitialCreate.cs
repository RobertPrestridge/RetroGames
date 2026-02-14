using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PocketTanks.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TanksMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShortCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Player1Name = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Player2Name = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Player1Score = table.Column<int>(type: "int", nullable: false),
                    Player2Score = table.Column<int>(type: "int", nullable: false),
                    TotalTurns = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TanksMatches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TanksMatches_ShortCode",
                table: "TanksMatches",
                column: "ShortCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TanksMatches");
        }
    }
}
