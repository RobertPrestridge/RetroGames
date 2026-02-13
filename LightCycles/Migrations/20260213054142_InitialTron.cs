using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LightCycles.Migrations
{
    /// <inheritdoc />
    public partial class InitialTron : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TronMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShortCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Player1Name = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Player2Name = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WinnerName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TickCount = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TronMatches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TronMatches_ShortCode",
                table: "TronMatches",
                column: "ShortCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TronMatches");
        }
    }
}
