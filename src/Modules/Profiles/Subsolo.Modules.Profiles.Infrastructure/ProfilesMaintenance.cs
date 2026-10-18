using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Profiles.Application;
using Subsolo.Modules.Profiles.Domain.Requests;

namespace Subsolo.Modules.Profiles.Infrastructure;

/// <summary>SPEC-0021 — gravações em lote direto no banco (exclusão da conta e limpeza periódica).</summary>
internal sealed class ProfilesMaintenance(ProfilesDbContext db) : IProfilesMaintenance
{
    public Task<int> ReleaseOwnedProfilesAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Profiles
            .Where(profile => profile.OwnerUserId == userId)
            .ExecuteUpdateAsync(update => update.SetProperty(profile => profile.OwnerUserId, (Guid?)null), cancellationToken);

    public Task<int> DeleteRequestsOfAsync(Guid userId, CancellationToken cancellationToken) =>
        db.ProfileRequests
            .Where(request => request.RequesterUserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteDecidedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken) =>
        db.ProfileRequests
            .Where(request => request.Status != ProfileRequestStatuses.Pending
                && request.DecidedAtUtc != null
                && request.DecidedAtUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
}
