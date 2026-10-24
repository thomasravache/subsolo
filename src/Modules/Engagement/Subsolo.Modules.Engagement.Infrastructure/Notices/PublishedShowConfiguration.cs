using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Subsolo.Modules.Engagement.Infrastructure.Notices;

/// <summary>
/// SPEC-0031 — uma linha de <c>engagement.published_shows</c> (§6): o show publicado, a cidade e os perfis dele, e quando
/// foi publicado. Sem dados de usuário (ADR-0008).
/// </summary>
internal sealed class PublishedShowRow
{
    public Guid ShowId { get; set; }

    public string CitySlug { get; set; } = string.Empty;

    public Guid[] ProfileIds { get; set; } = [];

    public DateTimeOffset PublishedAtUtc { get; set; }
}

/// <summary>
/// SPEC-0031 — tabela <c>engagement.published_shows(show_id PK, city_slug, profile_ids uuid[], published_at_utc)</c>
/// com o índice por <c>published_at_utc</c> (o resumo lê as últimas 24 h; a limpeza apaga as de mais de 7 dias).
/// </summary>
internal sealed class PublishedShowConfiguration : IEntityTypeConfiguration<PublishedShowRow>
{
    public void Configure(EntityTypeBuilder<PublishedShowRow> builder)
    {
        builder.ToTable("published_shows");
        builder.HasKey(row => row.ShowId);
        builder.Property(row => row.ShowId).HasColumnName("show_id").ValueGeneratedNever();
        builder.Property(row => row.CitySlug).HasColumnName("city_slug").HasColumnType("text");
        builder.Property(row => row.ProfileIds).HasColumnName("profile_ids").HasColumnType("uuid[]");
        builder.Property(row => row.PublishedAtUtc).HasColumnName("published_at_utc");
        builder.HasIndex(row => row.PublishedAtUtc);
    }
}
