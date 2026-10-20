using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiPlatformAdminBot.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledAtUtcToPendingBroadcast : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledAtUtc",
                table: "PendingBroadcastTBLs",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScheduledAtUtc",
                table: "PendingBroadcastTBLs");
        }
    }
}
