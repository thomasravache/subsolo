using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Subsolo.Modules.Events.Infrastructure.Migrations
{
    /// <summary>
    /// SPEC-0026 — índices do filtro de shows por perfil (aditivos; o modelo não muda): a casa (<c>venue_profile_id</c>) e
    /// o line-up (<c>jsonb</c>, GIN com <c>jsonb_path_ops</c> para o <c>lineup @&gt; [{"profileId": …}]</c>). O organizador
    /// já tem o <c>ix_shows_organizer</c>.
    /// </summary>
    public partial class ShowProfileIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE INDEX ix_shows_venue_profile ON events.shows (venue_profile_id);");
            migrationBuilder.Sql("CREATE INDEX ix_shows_lineup ON events.shows USING gin (lineup jsonb_path_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS events.ix_shows_lineup;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS events.ix_shows_venue_profile;");
        }
    }
}
