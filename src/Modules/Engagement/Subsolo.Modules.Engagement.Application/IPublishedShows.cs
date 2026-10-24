namespace Subsolo.Modules.Engagement.Application;

/// <summary>
/// SPEC-0031 — o registro curto dos shows publicados, base do resumo diário (§6:
/// <c>engagement.published_shows(show_id PK, city_slug, profile_ids uuid[], published_at_utc)</c>): sem dados de usuário
/// (ADR-0008); as linhas com mais de 7 dias são apagadas pelo job do resumo.
/// </summary>
internal interface IPublishedShows
{
    /// <summary>Registra o show publicado (idempotente pelo <see cref="PublishedShow.ShowId"/>).</summary>
    Task AddAsync(PublishedShow show, CancellationToken cancellationToken);

    /// <summary>Os shows publicados a partir de <paramref name="sinceUtc"/>.</summary>
    Task<IReadOnlyList<PublishedShow>> PublishedSinceAsync(DateTimeOffset sinceUtc, CancellationToken cancellationToken);

    /// <summary>Apaga as linhas publicadas antes de <paramref name="beforeUtc"/>; devolve quantas.</summary>
    Task<int> DeletePublishedBeforeAsync(DateTimeOffset beforeUtc, CancellationToken cancellationToken);
}

/// <summary>SPEC-0031 — uma linha de <c>engagement.published_shows</c>.</summary>
internal sealed record PublishedShow(Guid ShowId, string CitySlug, IReadOnlyList<Guid> ProfileIds, DateTimeOffset PublishedAtUtc);
