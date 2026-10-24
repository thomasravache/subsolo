using Subsolo.Modules.Engagement.Domain.Attendances;
using Subsolo.Modules.Engagement.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.GetAttendees;

/// <summary>
/// SPEC-0029 — <see cref="IAttendanceApi"/> sobre o <see cref="IAttendanceRepository"/>: as contagens por show (cada show
/// pedido aparece, 0 se ninguém marcou) e os ids de quem marcou um show, sem repetição (base do lembrete). SPEC-0036
/// (H32-04): os ids de vários shows numa leitura só.
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

    /// <summary>SPEC-0036 (H32-04) — cada show pedido aparece (sem "vou" → vazio), com os ids sem repetição.</summary>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetAttendeesAsync(
        IReadOnlyCollection<Guid> showIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(showIds);
        var wanted = showIds.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyCollection<Guid>>();
        }

        var attendees = await attendances.GetAttendeeIdsAsync(wanted, ct);
        return wanted.ToDictionary(
            id => id,
            id => (IReadOnlyCollection<Guid>)(attendees.TryGetValue(id, out var users) ? users.ToHashSet() : []));
    }
}
