using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Events.Infrastructure.Catalog;

/// <summary>SPEC-0033 (Emenda 2) — scaffold de <see cref="IShowHeadlines"/> sobre <c>events.shows</c>. Implementação pendente.</summary>
internal sealed class ShowHeadlines : IShowHeadlines
{
    public Task<IReadOnlyDictionary<Guid, ShowHeadline>> GetAsync(IReadOnlyCollection<Guid> showIds, CancellationToken ct) =>
        throw new NotImplementedException("SPEC-0033: IShowHeadlines.GetAsync");
}
