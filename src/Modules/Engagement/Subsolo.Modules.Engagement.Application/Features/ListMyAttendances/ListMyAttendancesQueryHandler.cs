using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.ListMyAttendances;

/// <summary>
/// SPEC-0029 — os "vou" do fã cujo show ainda existe e não começou (pelo <see cref="IEventsCatalog.GetShowStateAsync"/>;
/// cancelado ou adiado futuro continua na lista). <c>period</c> diferente de <c>upcoming</c> →
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
        var now = clock.GetUtcNow();
        List<Guid> upcoming = [];
        // Um fã marca poucos shows; o Events é lido show a show pela interface pública (ADR-0001).
        foreach (var attendance in mine)
        {
            if (await catalog.GetShowStateAsync(attendance.ShowId, cancellationToken) is { } show && show.StartsAtUtc > now)
            {
                upcoming.Add(attendance.ShowId);
            }
        }

        return Result.Success(new MyAttendancesResponse(upcoming));
    }
}
