using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiPlatformAdminBot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBotTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BotToken",
                schema: "BotCore",
                table: "PlatformConfigTBL");

            migrationBuilder.AddColumn<long>(
                name: "BotTokenId",
                schema: "BotCore",
                table: "PlatformConfigTBL",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BotTokenTBLs",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Explain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateInsert = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlatformName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TokenValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotTokenTBLs", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformConfigTBL_BotTokenId",
                schema: "BotCore",
                table: "PlatformConfigTBL",
                column: "BotTokenId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlatformConfigTBL_BotTokenTBLs_BotTokenId",
                schema: "BotCore",
                table: "PlatformConfigTBL",
                column: "BotTokenId",
                principalTable: "BotTokenTBLs",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlatformConfigTBL_BotTokenTBLs_BotTokenId",
                schema: "BotCore",
                table: "PlatformConfigTBL");

            migrationBuilder.DropTable(
                name: "BotTokenTBLs");

            migrationBuilder.DropIndex(
                name: "IX_PlatformConfigTBL_BotTokenId",
                schema: "BotCore",
                table: "PlatformConfigTBL");

            migrationBuilder.DropColumn(
                name: "BotTokenId",
                schema: "BotCore",
                table: "PlatformConfigTBL");

            migrationBuilder.AddColumn<string>(
                name: "BotToken",
                schema: "BotCore",
                table: "PlatformConfigTBL",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");
        }
    }
}
