using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiPlatformAdminBot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "BotCore");

            migrationBuilder.CreateTable(
                name: "AdminUserTBL",
                schema: "BotCore",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Explain = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    DateInsert = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsUploadingAlbum = table.Column<bool>(type: "bit", nullable: false),
                    CurrentAlbumId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUserTBL", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PlatformConfigTBL",
                schema: "BotCore",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Explain = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    DateInsert = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    PlatformName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BotToken = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    TargetChannelId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformConfigTBL", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "BroadcastLogTBL",
                schema: "BotCore",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Explain = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: true),
                    DateInsert = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    AdminUserId = table.Column<long>(type: "bigint", nullable: false),
                    ContentSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSuccessful = table.Column<bool>(type: "bit", nullable: false),
                    ErrorLog = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BroadcastLogTBL", x => x.ID);
                    table.ForeignKey(
                        name: "FK_BroadcastLogTBL_AdminUserTBL_AdminUserId",
                        column: x => x.AdminUserId,
                        principalSchema: "BotCore",
                        principalTable: "AdminUserTBL",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastLogTBL_AdminUserId",
                schema: "BotCore",
                table: "BroadcastLogTBL",
                column: "AdminUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BroadcastLogTBL",
                schema: "BotCore");

            migrationBuilder.DropTable(
                name: "PlatformConfigTBL",
                schema: "BotCore");

            migrationBuilder.DropTable(
                name: "AdminUserTBL",
                schema: "BotCore");
        }
    }
}
