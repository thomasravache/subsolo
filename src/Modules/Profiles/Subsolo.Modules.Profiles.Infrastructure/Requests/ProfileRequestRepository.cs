using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Profiles.Domain.Requests;

namespace Subsolo.Modules.Profiles.Infrastructure.Requests;

/// <summary>SPEC-0021 — pedidos de perfil e reivindicações no schema <c>profiles</c> (rastreados: a decisão muda a situação).</summary>
internal sealed class ProfileRequestRepository(ProfilesDbContext db) : IProfileRequestRepository
{
    public async Task<ProfileRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await db.ProfileRequests.FindAsync([id], cancellationToken);

    public Task<bool> HasPendingAsync(Guid requesterUserId, CancellationToken cancellationToken) =>
        db.ProfileRequests.AnyAsync(
            request => request.RequesterUserId == requesterUserId && request.Status == ProfileRequestStatuses.Pending,
            cancellationToken);

    public void Add(ProfileRequest request) => db.ProfileRequests.Add(request);
}
