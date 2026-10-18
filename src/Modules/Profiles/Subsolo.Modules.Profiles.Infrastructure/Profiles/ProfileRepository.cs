using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Profiles.Domain.Profiles;

namespace Subsolo.Modules.Profiles.Infrastructure.Profiles;

/// <summary>SPEC-0021 — perfis no schema <c>profiles</c> (rastreados: a aprovação muda o dono).</summary>
internal sealed class ProfileRepository(ProfilesDbContext db) : IProfileRepository
{
    public async Task<Profile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Profiles.FindAsync([id], cancellationToken);

    public Task<bool> ExistsAsync(string type, string slug, CancellationToken cancellationToken) =>
        db.Profiles.AnyAsync(profile => profile.Type == type && profile.Slug == slug, cancellationToken);

    public void Add(Profile profile) => db.Profiles.Add(profile);
}
