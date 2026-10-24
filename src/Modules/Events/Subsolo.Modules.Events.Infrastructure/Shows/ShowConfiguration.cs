using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Subsolo.Modules.Events.Domain.Flyers;
using Subsolo.Modules.Events.Domain.Shows;

namespace Subsolo.Modules.Events.Infrastructure.Shows;

/// <summary>
/// SPEC-0023 — tabela <c>events.shows</c> (§6): slug único por cidade, início em UTC, organizador, casa (perfil ou só o
/// nome), line-up em <c>jsonb</c> (<c>[{profileId, name}]</c>), estilos em <c>text[]</c>, preço, link do ingresso,
/// flyer (FK para <c>events.flyers</c>), situação, quem cadastrou e as datas; índices da agenda (cidade + início), do
/// organizador e de quem cadastrou.
/// </summary>
internal sealed class ShowConfiguration : IEntityTypeConfiguration<Show>
{
    /// <summary>Índice único do slug na cidade (o <c>EventsDbContext</c> traduz a violação para o 409).</summary>
    public const string CitySlugIndex = "IX_shows_city_slug";

    private const int SlugMaxLength = 80;

    private const int CityMaxLength = 60;

    private const int StatusMaxLength = 20;

    private const int TicketUrlMaxLength = 2000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Show> builder)
    {
        builder.ToTable("shows");
        builder.HasKey(show => show.Id);
        builder.Property(show => show.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(show => show.Slug).HasColumnName("slug").HasMaxLength(SlugMaxLength);
        builder.Property(show => show.City).HasColumnName("city").HasMaxLength(CityMaxLength);
        builder.Property(show => show.Title).HasColumnName("title").HasMaxLength(Show.TitleMaxLength);
        builder.Property(show => show.StartsAtUtc).HasColumnName("starts_at_utc");
        builder.Property(show => show.OrganizerProfileId).HasColumnName("organizer_profile_id");
        builder.ComplexProperty(show => show.Venue, venue =>
        {
            venue.Property(item => item.ProfileId).HasColumnName("venue_profile_id");
            venue.Property(item => item.Name).HasColumnName("venue_name").HasMaxLength(Show.NameMaxLength);
        });
        builder.Property(show => show.Lineup)
            .HasColumnName("lineup")
            .HasColumnType("jsonb")
            .HasConversion(
                lineup => JsonSerializer.Serialize(lineup, Json),
                json => JsonSerializer.Deserialize<List<ShowLineupItem>>(json, Json) ?? new List<ShowLineupItem>(),
                new ValueComparer<IReadOnlyList<ShowLineupItem>>(
                    (left, right) => left!.SequenceEqual(right!),
                    lineup => lineup.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                    lineup => lineup.ToList()));
        builder.PrimitiveCollection(show => show.Styles).HasColumnName("styles");
        builder.Property(show => show.Price).HasColumnName("price").HasMaxLength(Show.PriceMaxLength);
        builder.Property(show => show.TicketUrl).HasColumnName("ticket_url").HasMaxLength(TicketUrlMaxLength);
        builder.Property(show => show.FlyerId).HasColumnName("flyer_id").HasMaxLength(Flyer.IdLength).IsFixedLength();
        builder.Property(show => show.Status).HasColumnName("status").HasMaxLength(StatusMaxLength);
        builder.Property(show => show.CreatedBy).HasColumnName("created_by");
        builder.Property(show => show.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(show => show.UpdatedAtUtc).HasColumnName("updated_at_utc");
        // SPEC-0033: oculto pela moderação (nulo = visível).
        builder.Property(show => show.HiddenAtUtc).HasColumnName("hidden_at_utc");
        builder.Ignore(show => show.ProfileIds);
        builder.Ignore(show => show.IsHidden);

        builder.HasOne<Flyer>().WithMany().HasForeignKey(show => show.FlyerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(show => new { show.City, show.Slug }).IsUnique().HasDatabaseName(CitySlugIndex);
        builder.HasIndex(show => new { show.City, show.StartsAtUtc }).HasDatabaseName("ix_shows_city_starts_at");
        builder.HasIndex(show => show.OrganizerProfileId).HasDatabaseName("ix_shows_organizer");
        builder.HasIndex(show => show.CreatedBy).HasDatabaseName("ix_shows_created_by");
    }
}
