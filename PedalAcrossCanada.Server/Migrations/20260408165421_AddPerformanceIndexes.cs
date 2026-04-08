using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PedalAcrossCanada.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Participants_EventId_Status",
                table: "Participants",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_EventId_Status_CountsTowardTotal",
                table: "Activities",
                columns: new[] { "EventId", "Status", "CountsTowardTotal" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Participants_EventId_Status",
                table: "Participants");

            migrationBuilder.DropIndex(
                name: "IX_Activities_EventId_Status_CountsTowardTotal",
                table: "Activities");
        }
    }
}
