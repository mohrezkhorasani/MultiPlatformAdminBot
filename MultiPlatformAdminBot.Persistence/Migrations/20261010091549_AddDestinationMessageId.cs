using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiPlatformAdminBot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDestinationMessageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DestinationMessageId",
                table: "PostReportTBLs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DestinationMessageId",
                table: "PostReportTBLs");
        }
    }
}
