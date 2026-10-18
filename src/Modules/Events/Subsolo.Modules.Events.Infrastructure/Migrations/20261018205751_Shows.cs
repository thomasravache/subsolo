using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Subsolo.Modules.Events.Infrastructure.Migrations
{
    /// <summary>
    /// SPEC-0023 — tabela <c>events.shows</c> (aditiva): slug único por cidade, início em UTC, organizador, casa, line-up
    /// em <c>jsonb</c>, estilos, preço, link do ingresso, flyer (FK para <c>events.flyers</c>), situação e quem cadastrou,
    /// com os índices da agenda (cidade + início), do organizador e de quem cadastrou.
    /// </summary>
    public partial class Shows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shows",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    city = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    starts_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    organizer_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lineup = table.Column<string>(type: "jsonb", nullable: false),
                    styles = table.Column<string[]>(type: "text[]", nullable: false),
                    price = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ticket_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    flyer_id = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    venue_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    venue_profile_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shows", x => x.id);
                    table.ForeignKey(
                        name: "FK_shows_flyers_flyer_id",
                        column: x => x.flyer_id,
                        principalSchema: "events",
                        principalTable: "flyers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shows_city_slug",
                schema: "events",
                table: "shows",
                columns: new[] { "city", "slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shows_city_starts_at",
                schema: "events",
                table: "shows",
                columns: new[] { "city", "starts_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_shows_created_by",
                schema: "events",
                table: "shows",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_shows_flyer_id",
                schema: "events",
                table: "shows",
                column: "flyer_id");

            migrationBuilder.CreateIndex(
                name: "ix_shows_organizer",
                schema: "events",
                table: "shows",
                column: "organizer_profile_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shows",
                schema: "events");
        }
    }
}
