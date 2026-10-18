using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Profiles.Infrastructure;

/// <summary>
/// SPEC-0023 (Emenda v2) — <see cref="IShowProfiles"/>, o contrato do Events para o cadastro de show, sobre o schema
/// <c>profiles</c> (todo perfil gravado já é aprovado; nunca o contato nem a prova dos pedidos).
/// </summary>
internal sealed class ShowProfiles(ProfilesDbContext db) : IShowProfiles
{
    public async Task<IReadOnlyList<ShowProfile>> GetOwnedProfilesAsync(Guid userId, CancellationToken ct) =>
        await db.Profiles.AsNoTracking()
            .Where(profile => profile.OwnerUserId == userId)
            .OrderBy(profile => profile.Name)
            .Select(profile => new ShowProfile(profile.Id, profile.Type, profile.Name, profile.Slug, profile.City, profile.OwnerUserId))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, ShowProfile>> GetManyAsync(IReadOnlyCollection<Guid> profileIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(profileIds);
        if (profileIds.Count == 0)
        {
            return new Dictionary<Guid, ShowProfile>();
        }

        var ids = profileIds.Distinct().ToList();
        return await db.Profiles.AsNoTracking()
            .Where(profile => ids.Contains(profile.Id))
            .Select(profile => new ShowProfile(profile.Id, profile.Type, profile.Name, profile.Slug, profile.City, profile.OwnerUserId))
            .ToDictionaryAsync(profile => profile.Id, ct);
    }
}
