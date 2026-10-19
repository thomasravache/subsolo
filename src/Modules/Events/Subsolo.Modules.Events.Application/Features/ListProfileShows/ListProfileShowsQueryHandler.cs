using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application.Features.ListMyShows;

namespace Subsolo.Modules.Events.Application.Features.ListProfileShows;

/// <summary>
/// SPEC-0026 — <c>period</c> fora de <c>upcoming</c> e <c>past</c> → <c>events.invalid_query</c> (sem consultar o banco);
/// "próximos" são os que começam a partir de 6 h atrás (a mesma tolerância da agenda e de "Meus shows"), até 50 por data
/// crescente; "passados" os de antes, até 20, mais recentes primeiro.
/// </summary>
internal sealed class ListProfileShowsQueryHandler(IShowsReadModel shows, TimeProvider clock)
    : IQueryHandler<ListProfileShowsQuery, ProfileShowsResponse>
{
    public async Task<Result<ProfileShowsResponse>> Handle(ListProfileShowsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Period is not (ListProfileShowsQuery.Upcoming or ListProfileShowsQuery.Past))
        {
            return Result.Failure<ProfileShowsResponse>(EventsErrors.InvalidQueryError("period", "Use upcoming ou past."));
        }

        var upcoming = query.Period == ListProfileShowsQuery.Upcoming;
        var found = await shows.ListByProfileAsync(
            query.ProfileId,
            clock.GetUtcNow() - ListMyShowsQueryHandler.Grace,
            upcoming,
            upcoming ? ListProfileShowsQuery.UpcomingLimit : ListProfileShowsQuery.PastLimit,
            cancellationToken);
        return Result.Success(new ProfileShowsResponse(found));
    }
}
