using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Profiles.IntegrationEvents;

namespace Subsolo.Modules.Profiles.Infrastructure;

/// <summary>SPEC-0021 — <see cref="IProfilesApi"/> sobre o schema <c>profiles</c> (nunca o contato nem a prova).</summary>
internal sealed class ProfilesApi(ProfilesDbContext db) : IProfilesApi
{
    public async Task<IReadOnlyList<ProfileSummary>> GetOwnedProfilesAsync(Guid userId, CancellationToken ct) =>
        await db.Profiles.AsNoTracking()
            .Where(profile => profile.OwnerUserId == userId)
            .OrderBy(profile => profile.Name)
            .Select(profile => new ProfileSummary(profile.Id, profile.Type, profile.Name, profile.Slug, profile.City, profile.OwnerUserId))
            .ToListAsync(ct);

    public Task<ProfileSummary?> GetAsync(Guid profileId, CancellationToken ct) =>
        db.Profiles.AsNoTracking()
            .Where(profile => profile.Id == profileId)
            .Select(profile => new ProfileSummary(profile.Id, profile.Type, profile.Name, profile.Slug, profile.City, profile.OwnerUserId))
            .SingleOrDefaultAsync(ct);
}
