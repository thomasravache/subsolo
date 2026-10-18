using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Events.Application.Features.ListMyShows;

/// <summary>
/// SPEC-0023 — período fora de <c>upcoming</c> e <c>past</c> → <c>events.invalid_period</c>; "próximos" são os que
/// começam a partir de 6 h atrás (como na agenda), "passados" os de antes.
/// </summary>
internal sealed class ListMyShowsQueryHandler(IShowsReadModel shows, IShowProfiles profiles, TimeProvider clock)
    : IQueryHandler<ListMyShowsQuery, IReadOnlyList<MyShowResponse>>
{
    /// <summary>Quanto tempo depois do início um show ainda conta como "próximo".</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromHours(6);

    public async Task<Result<IReadOnlyList<MyShowResponse>>> Handle(ListMyShowsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var period = query.Period ?? ListMyShowsQuery.Upcoming;
        if (period is not (ListMyShowsQuery.Upcoming or ListMyShowsQuery.Past))
        {
            return Result.Failure<IReadOnlyList<MyShowResponse>>(EventsErrors.InvalidPeriodError());
        }

        var boundary = clock.GetUtcNow() - Grace;
        var upcoming = period == ListMyShowsQuery.Upcoming;
        if (query.IsAdmin)
        {
            return Result.Success(await shows.ListMineAsync([], query.UserId, boundary, upcoming, cancellationToken));
        }

        var owned = await profiles.GetOwnedProfilesAsync(query.UserId, cancellationToken);
        if (owned.Count == 0)
        {
            return Result.Success<IReadOnlyList<MyShowResponse>>([]);
        }

        return Result.Success(await shows.ListMineAsync(
            [.. owned.Select(profile => profile.Id)], createdBy: null, boundary, upcoming, cancellationToken));
    }
}
