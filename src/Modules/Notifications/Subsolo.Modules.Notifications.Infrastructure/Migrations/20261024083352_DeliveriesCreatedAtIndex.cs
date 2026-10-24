using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Subsolo.Modules.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DeliveriesCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_deliveries_created_at_utc",
                schema: "notifications",
                table: "deliveries",
                column: "created_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_deliveries_created_at_utc",
                schema: "notifications",
                table: "deliveries");
        }
    }
}
