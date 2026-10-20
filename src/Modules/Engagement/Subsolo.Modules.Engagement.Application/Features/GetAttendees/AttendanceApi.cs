using Subsolo.Modules.Engagement.Domain.Attendances;
using Subsolo.Modules.Engagement.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.GetAttendees;

/// <summary>
/// SPEC-0029 — <see cref="IAttendanceApi"/> sobre o <see cref="IAttendanceRepository"/>: as contagens por show (cada show
/// pedido aparece, 0 se ninguém marcou) e os ids de quem marcou um show, sem repetição (base do lembrete).
/// </summary>
internal sealed class AttendanceApi(IAttendanceRepository attendances) : IAttendanceApi
{
    public async Task<IReadOnlyDictionary<Guid, int>> CountAsync(IReadOnlyCollection<Guid> showIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(showIds);
        var wanted = showIds.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var counts = await attendances.CountAsync(wanted, ct);
        return wanted.ToDictionary(id => id, id => counts.GetValueOrDefault(id));
    }

    public async Task<IReadOnlyCollection<Guid>> GetAttendeesAsync(Guid showId, CancellationToken ct) =>
        (await attendances.GetAttendeeIdsAsync(showId, ct)).ToHashSet();
}
