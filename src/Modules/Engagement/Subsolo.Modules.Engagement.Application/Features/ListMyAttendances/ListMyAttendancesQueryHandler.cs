using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.ListMyAttendances;

/// <summary>
/// SPEC-0029 — os "vou" do fã cujo show ainda existe e não começou (cancelado ou adiado futuro continua na lista), na
/// ordem do "vou"; SPEC-0032 (H29-01): as situações vêm numa leitura em lote (<see cref="IEventsCatalog.GetShowStatesAsync"/>). <c>period</c> diferente de <c>upcoming</c> →
/// <see cref="EngagementErrors.InvalidQuery"/>.
/// </summary>
internal sealed class ListMyAttendancesQueryHandler(
    IAttendancesReadModel attendances,
    IEventsCatalog catalog,
    TimeProvider clock) : IQueryHandler<ListMyAttendancesQuery, MyAttendancesResponse>
{
    public const string Upcoming = "upcoming";

    public async Task<Result<MyAttendancesResponse>> Handle(ListMyAttendancesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Period is not null && !string.Equals(query.Period, Upcoming, StringComparison.Ordinal))
        {
            return Result.Failure<MyAttendancesResponse>(
                EngagementErrors.InvalidQueryError("period", "O período aceito é \"upcoming\"."));
        }

        var mine = await attendances.ListOfAsync(query.UserId, cancellationToken);
        if (mine.Count == 0)
        {
            return Result.Success(new MyAttendancesResponse([]));
        }

        var now = clock.GetUtcNow();
        // SPEC-0032 (H29-01): as situações vêm do Events numa leitura em lote (ADR-0001: pela interface pública).
        var states = await catalog.GetShowStatesAsync([.. mine.Select(attendance => attendance.ShowId)], cancellationToken);
        List<Guid> upcoming =
        [
            .. mine
                .Where(attendance => states.TryGetValue(attendance.ShowId, out var show) && show.StartsAtUtc > now)
                .Select(attendance => attendance.ShowId),
        ];

        return Result.Success(new MyAttendancesResponse(upcoming));
    }
}
