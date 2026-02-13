using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicTacToe.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShortCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    BoardState = table.Column<string>(type: "nchar(9)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentTurn = table.Column<string>(type: "nchar(1)", nullable: false),
                    PlayerXConnectionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PlayerOConnectionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PlayerXSessionId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PlayerOSessionId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MoveCount = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Games_PlayerOSessionId",
                table: "Games",
                column: "PlayerOSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_PlayerXSessionId",
                table: "Games",
                column: "PlayerXSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_ShortCode",
                table: "Games",
                column: "ShortCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_Status_CreatedAt",
                table: "Games",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Games");
        }
    }
}
