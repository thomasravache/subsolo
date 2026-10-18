using Subsolo.Modules.Events.Domain.Styles;

namespace Subsolo.Modules.Events.Infrastructure.Styles;

/// <summary>SPEC-0017 — estilos no schema <c>events</c>.</summary>
#pragma warning disable CS9113 // SPEC-0017: andaime do contrato; o DbContext é usado na implementação.
internal sealed class StyleRepository(EventsDbContext db) : IStyleRepository
#pragma warning restore CS9113
{
    public Task<Style?> GetBySlugAsync(string slug, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0017: StyleRepository ainda não implementado.");

    public Task<IReadOnlyList<Style>> ListAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0017: StyleRepository ainda não implementado.");

    public void Add(Style style) =>
        throw new NotImplementedException("SPEC-0017: StyleRepository ainda não implementado.");
}
