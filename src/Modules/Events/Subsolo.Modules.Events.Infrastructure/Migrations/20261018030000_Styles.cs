using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Subsolo.Modules.Events.Infrastructure.Migrations
{
    /// <summary>
    /// SPEC-0017 — tabela <c>events.styles</c> (slug único) e os 5 estilos iniciais (migração de dados: valem em todo
    /// ambiente, produção inclusive), com ids fixos.
    /// </summary>
    public partial class Styles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "styles",
                schema: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_styles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_styles_slug",
                schema: "events",
                table: "styles",
                column: "slug",
                unique: true);

            migrationBuilder.InsertData(
                schema: "events",
                table: "styles",
                columns: new[] { "id", "slug", "name" },
                values: new object[,]
                {
                    { new Guid("7d0c5e2a-3f4b-4c1d-9e8f-17a000000001"), "death", "Death" },
                    { new Guid("7d0c5e2a-3f4b-4c1d-9e8f-17a000000002"), "black", "Black" },
                    { new Guid("7d0c5e2a-3f4b-4c1d-9e8f-17a000000003"), "grind", "Grind" },
                    { new Guid("7d0c5e2a-3f4b-4c1d-9e8f-17a000000004"), "doom", "Doom" },
                    { new Guid("7d0c5e2a-3f4b-4c1d-9e8f-17a000000005"), "thrash", "Thrash" },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "styles",
                schema: "events");
        }
    }
}
