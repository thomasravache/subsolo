using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Subsolo.Modules.Events.Infrastructure.Migrations
{
    /// <summary>
    /// SPEC-0036 (H32-04) — índice parcial dos shows agendados por início (<c>ix_shows_scheduled_starts_at</c>): serve a
    /// consulta de hora em hora do lembrete na véspera (<c>status = 'scheduled'</c> e <c>starts_at_utc</c> numa janela,
    /// sem a cidade que o <c>ix_shows_city_starts_at</c> exige).
    /// </summary>
    public partial class ScheduledShowsStartsAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_shows_scheduled_starts_at",
                schema: "events",
                table: "shows",
                column: "starts_at_utc",
                filter: "status = 'scheduled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_shows_scheduled_starts_at",
                schema: "events",
                table: "shows");
        }
    }
}
