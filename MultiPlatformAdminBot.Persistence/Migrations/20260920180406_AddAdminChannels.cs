using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiPlatformAdminBot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                schema: "BotCore",
                table: "AdminUserTBL",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdminChannelTBLs",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminUserId = table.Column<long>(type: "bigint", nullable: false),
                    ChannelId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Explain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateInsert = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminChannelTBLs", x => x.ID);
                    table.ForeignKey(
                        name: "FK_AdminChannelTBLs_AdminUserTBL_AdminUserId",
                        column: x => x.AdminUserId,
                        principalSchema: "BotCore",
                        principalTable: "AdminUserTBL",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdminChannelTBLs_PlatformConfigTBL_ChannelId",
                        column: x => x.ChannelId,
                        principalSchema: "BotCore",
                        principalTable: "PlatformConfigTBL",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminChannelTBLs_AdminUserId_ChannelId",
                table: "AdminChannelTBLs",
                columns: new[] { "AdminUserId", "ChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminChannelTBLs_ChannelId",
                table: "AdminChannelTBLs",
                column: "ChannelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminChannelTBLs");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                schema: "BotCore",
                table: "AdminUserTBL");
        }
    }
}
