using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Modules.Events.Application.Features.ListProfileShows;

/// <summary>SPEC-0026 — esqueleto do Test-writer: o Implementer escreve a consulta (G2).</summary>
internal sealed class ListProfileShowsQueryHandler(IShowsReadModel shows, TimeProvider clock)
    : IQueryHandler<ListProfileShowsQuery, ProfileShowsResponse>
{
    private readonly IShowsReadModel _shows = shows;

    private readonly TimeProvider _clock = clock;

    public Task<Result<ProfileShowsResponse>> Handle(ListProfileShowsQuery query, CancellationToken cancellationToken) =>
        throw new NotImplementedException($"SPEC-0026: ListProfileShows ainda não implementado ({_shows.GetType().Name}, {_clock.GetType().Name}).");
}
