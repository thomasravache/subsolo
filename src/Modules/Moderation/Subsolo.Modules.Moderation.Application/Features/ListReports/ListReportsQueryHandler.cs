using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Moderation.Application.Features.ListReports;

/// <summary>
/// SPEC-0033 — lê as denúncias pendentes, agrupa por show (<see cref="ReportQueueGrouper"/>) e completa cada item com os
/// dados do show numa leitura só do Events (<see cref="IShowHeadlines"/>, Emenda 2). Show que o Events não devolve
/// (inexistente ou já oculto) fica fora da fila.
/// </summary>
internal sealed class ListReportsQueryHandler(IReportsReadModel reports, IShowHeadlines headlines)
    : IQueryHandler<ListReportsQuery, IReadOnlyList<ReportQueueItem>>
{
    public async Task<Result<IReadOnlyList<ReportQueueItem>>> Handle(ListReportsQuery query, CancellationToken cancellationToken)
    {
        var groups = ReportQueueGrouper.Group(await reports.ListPendingAsync(cancellationToken));
        if (groups.Count == 0)
        {
            return Result.Success<IReadOnlyList<ReportQueueItem>>([]);
        }

        var shows = await headlines.GetAsync([.. groups.Select(group => group.ShowId)], cancellationToken);
        return Result.Success<IReadOnlyList<ReportQueueItem>>(
        [
            .. groups
                .Where(group => shows.ContainsKey(group.ShowId))
                .Select(group =>
                {
                    var show = shows[group.ShowId];
                    return new ReportQueueItem(
                        group.ShowId,
                        show.Title,
                        show.CitySlug,
                        show.Slug,
                        show.StartsAtUtc,
                        show.VenueName,
                        new ReportCountsResponse(
                            group.Counts.Fake,
                            group.Counts.CancelledUnannounced,
                            group.Counts.WrongInfo,
                            group.Counts.Other),
                        group.LatestDetails,
                        group.FirstReportedAtUtc,
                        show.TimeZone);
                }),
        ]);
    }
}
