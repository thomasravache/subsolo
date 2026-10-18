using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Subsolo.Modules.Profiles.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Concurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "profiles",
                table: "profiles",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "profiles",
                table: "profile_requests",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_profile_requests_pending_requester",
                schema: "profiles",
                table: "profile_requests",
                column: "requester_user_id",
                unique: true,
                filter: "status = 'pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_profile_requests_pending_requester",
                schema: "profiles",
                table: "profile_requests");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "profiles",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "profiles",
                table: "profile_requests");
        }
    }
}
