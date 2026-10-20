using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiPlatformAdminBot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryLogTBL : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeliveryLogTBL",
                schema: "BotCore",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Explain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateInsert = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    BroadcastLogId = table.Column<long>(type: "bigint", nullable: true),
                    AdminUserId = table.Column<long>(type: "bigint", nullable: false),
                    PlatformName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetChannelId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<int>(type: "int", nullable: false),
                    ContentText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    MediaPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MediaFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextRetryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryLogTBL", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryLogTBL_AdminUserId",
                schema: "BotCore",
                table: "DeliveryLogTBL",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryLogTBL_Status_NextRetryAt",
                schema: "BotCore",
                table: "DeliveryLogTBL",
                columns: new[] { "Status", "NextRetryAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryLogTBL",
                schema: "BotCore");
        }
    }
}
