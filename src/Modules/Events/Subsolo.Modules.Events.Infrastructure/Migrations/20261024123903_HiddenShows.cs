using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Subsolo.Modules.Events.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HiddenShows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "hidden_at_utc",
                schema: "events",
                table: "shows",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hidden_at_utc",
                schema: "events",
                table: "shows");
        }
    }
}
