using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Events.Infrastructure.Catalog;

/// <summary>SPEC-0021 — implementação de <see cref="IEventsCatalog"/> sobre as cidades e estilos do schema <c>events</c>.</summary>
internal sealed class EventsCatalog : IEventsCatalog
{
    public Task<bool> CityExistsAsync(string slug, CancellationToken ct) =>
        throw new NotImplementedException("SPEC-0021: IEventsCatalog.CityExistsAsync ainda não implementado.");

    public Task<bool> StylesExistAsync(IReadOnlyCollection<string> slugs, CancellationToken ct) =>
        throw new NotImplementedException("SPEC-0021: IEventsCatalog.StylesExistAsync ainda não implementado.");
}
