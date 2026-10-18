using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Subsolo.Modules.Events.Infrastructure.Migrations
{
    /// <summary>
    /// SPEC-0022 — tabela <c>events.flyers</c> (aditiva): id <c>char(64)</c> (SHA-256 do original), quem enviou, quando e
    /// quando foi ligado a um show, com o índice parcial dos não ligados para a limpeza.
    /// </summary>
    public partial class Flyers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "flyers",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attached_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flyers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_flyers_unattached",
                schema: "events",
                table: "flyers",
                column: "created_at_utc",
                filter: "attached_at_utc IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "flyers",
                schema: "events");
        }
    }
}
