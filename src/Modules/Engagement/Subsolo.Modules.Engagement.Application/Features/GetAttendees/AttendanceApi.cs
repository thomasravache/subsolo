using Subsolo.Modules.Engagement.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.GetAttendees;

/// <summary>SPEC-0029 — esqueleto (G1) do <see cref="IAttendanceApi"/>; a implementação entra no G2.</summary>
internal sealed class AttendanceApi : IAttendanceApi
{
    public Task<IReadOnlyDictionary<Guid, int>> CountAsync(IReadOnlyCollection<Guid> showIds, CancellationToken ct) =>
        throw new NotImplementedException("SPEC-0029: CountAsync ainda não implementado");

    public Task<IReadOnlyCollection<Guid>> GetAttendeesAsync(Guid showId, CancellationToken ct) =>
        throw new NotImplementedException("SPEC-0029: GetAttendeesAsync ainda não implementado");
}
