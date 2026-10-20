using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiPlatformAdminBot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPostReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PostReportTBLs",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Explain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateInsert = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdminChannelId = table.Column<long>(type: "bigint", nullable: false),
                    IsSuccessful = table.Column<bool>(type: "bit", nullable: false),
                    ErrorLog = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostReportTBLs", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PostReportTBLs_AdminChannelTBLs_AdminChannelId",
                        column: x => x.AdminChannelId,
                        principalTable: "AdminChannelTBLs",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostReportMetaDataTBLs",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Explain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateInsert = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PostReportId = table.Column<long>(type: "bigint", nullable: false),
                    MetaKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MetaValue = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostReportMetaDataTBLs", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PostReportMetaDataTBLs_PostReportTBLs_PostReportId",
                        column: x => x.PostReportId,
                        principalTable: "PostReportTBLs",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PostReportMetaDataTBLs_PostReportId",
                table: "PostReportMetaDataTBLs",
                column: "PostReportId");

            migrationBuilder.CreateIndex(
                name: "IX_PostReportTBLs_AdminChannelId",
                table: "PostReportTBLs",
                column: "AdminChannelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PostReportMetaDataTBLs");

            migrationBuilder.DropTable(
                name: "PostReportTBLs");
        }
    }
}
