using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Subsolo.Modules.Events.Infrastructure.Migrations
{
    /// <summary>
    /// SPEC-0024 (Emenda v2, b) — as medidas da variante <c>full</c> em <c>events.flyers</c> (aditiva): <c>full_width</c>
    /// e <c>full_height</c>, nulas nos flyers enviados antes (a página do show usa a proporção 3:4).
    /// </summary>
    public partial class FlyerSizes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "full_height",
                schema: "events",
                table: "flyers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "full_width",
                schema: "events",
                table: "flyers",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "full_height",
                schema: "events",
                table: "flyers");

            migrationBuilder.DropColumn(
                name: "full_width",
                schema: "events",
                table: "flyers");
        }
    }
}
