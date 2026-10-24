using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Subsolo.Modules.Engagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PublishedShows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "published_shows",
                schema: "engagement",
                columns: table => new
                {
                    show_id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_slug = table.Column<string>(type: "text", nullable: false),
                    profile_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_published_shows", x => x.show_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_published_shows_published_at_utc",
                schema: "engagement",
                table: "published_shows",
                column: "published_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "published_shows",
                schema: "engagement");
        }
    }
}
