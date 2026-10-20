using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiPlatformAdminBot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OwnerId",
                table: "BotTokenTBLs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentRoute",
                schema: "BotCore",
                table: "AdminUserTBL",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "HasPanelAccess",
                schema: "BotCore",
                table: "AdminUserTBL",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxChannelsPerToken",
                schema: "BotCore",
                table: "AdminUserTBL",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxTotalChannels",
                schema: "BotCore",
                table: "AdminUserTBL",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "OwnerId",
                schema: "BotCore",
                table: "AdminUserTBL",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdminRequestTBLs",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Explain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateInsert = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BanExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminRequestTBLs", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BotTokenTBLs_OwnerId",
                table: "BotTokenTBLs",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_BotTokenTBLs_AdminUserTBL_OwnerId",
                table: "BotTokenTBLs",
                column: "OwnerId",
                principalSchema: "BotCore",
                principalTable: "AdminUserTBL",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BotTokenTBLs_AdminUserTBL_OwnerId",
                table: "BotTokenTBLs");

            migrationBuilder.DropTable(
                name: "AdminRequestTBLs");

            migrationBuilder.DropIndex(
                name: "IX_BotTokenTBLs_OwnerId",
                table: "BotTokenTBLs");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "BotTokenTBLs");

            migrationBuilder.DropColumn(
                name: "CurrentRoute",
                schema: "BotCore",
                table: "AdminUserTBL");

            migrationBuilder.DropColumn(
                name: "HasPanelAccess",
                schema: "BotCore",
                table: "AdminUserTBL");

            migrationBuilder.DropColumn(
                name: "MaxChannelsPerToken",
                schema: "BotCore",
                table: "AdminUserTBL");

            migrationBuilder.DropColumn(
                name: "MaxTotalChannels",
                schema: "BotCore",
                table: "AdminUserTBL");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                schema: "BotCore",
                table: "AdminUserTBL");
        }
    }
}
