using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Profiles.Application;
using Subsolo.Modules.Profiles.Application.Features.ListMyProfiles;
using Subsolo.Modules.Profiles.Application.Features.ListProfileRequests;
using Subsolo.Modules.Profiles.Application.Features.SearchClaimableProfiles;
using Subsolo.Modules.Profiles.Domain.Profiles;
using Subsolo.Modules.Profiles.Domain.Requests;

namespace Subsolo.Modules.Profiles.Infrastructure;

/// <summary>SPEC-0021 — as leituras das consultas do módulo, sem rastreamento.</summary>
internal sealed class ProfilesReadModel(ProfilesDbContext db) : IProfilesReadModel
{
    public async Task<IReadOnlyList<ClaimableProfileResponse>> SearchClaimableAsync(
        string term, string termSlug, string? type, int limit, CancellationToken cancellationToken)
    {
        var pattern = $"%{EscapeLike(term)}%";
        var query = db.Profiles.AsNoTracking().Where(profile => profile.OwnerUserId == null);
        if (type is not null)
        {
            query = query.Where(profile => profile.Type == type);
        }

        query = termSlug.Length > 0
            ? query.Where(profile => EF.Functions.ILike(profile.Name, pattern, "\\") || profile.Slug.Contains(termSlug))
            : query.Where(profile => EF.Functions.ILike(profile.Name, pattern, "\\"));

        return await query
            .OrderBy(profile => profile.Name)
            .ThenBy(profile => profile.Id)
            .Take(limit)
            .Select(profile => new ClaimableProfileResponse(profile.Id, profile.Type, profile.Name, profile.City, profile.Styles))
            .ToListAsync(cancellationToken);
    }

    public async Task<MyProfilesResponse> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var owned = await db.Profiles.AsNoTracking()
            .Where(profile => profile.OwnerUserId == userId)
            .OrderBy(profile => profile.Name)
            .Select(profile => new MyProfileResponse(profile.Id, profile.Type, profile.Name, profile.Slug, profile.City))
            .ToListAsync(cancellationToken);
        var requests = await db.ProfileRequests.AsNoTracking()
            .Where(request => request.RequesterUserId == userId)
            .OrderByDescending(request => request.CreatedAtUtc)
            .ThenByDescending(request => request.Id)
            .ToListAsync(cancellationToken);
        var claimed = await ProfilesByIdAsync(requests, cancellationToken);

        return new MyProfilesResponse(
            owned,
            [
                .. requests.Select(request =>
                {
                    var (type, name) = Describe(request, claimed);
                    return new MyProfileRequestResponse(
                        request.Id, request.Kind, name, type, request.Status, request.Reason, request.CreatedAtUtc);
                }),
            ]);
    }

    public async Task<IReadOnlyList<PendingProfileRequest>> ListPendingAsync(CancellationToken cancellationToken)
    {
        var pending = await db.ProfileRequests.AsNoTracking()
            .Where(request => request.Status == ProfileRequestStatuses.Pending)
            .OrderBy(request => request.CreatedAtUtc)
            .ThenBy(request => request.Id)
            .ToListAsync(cancellationToken);
        var claimed = await ProfilesByIdAsync(pending, cancellationToken);

        return
        [
            .. pending.Select(request =>
            {
                var draft = request.Draft;
                var profile = request.ProfileId is { } id ? claimed.GetValueOrDefault(id) : null;
                var links = draft?.Links ?? profile?.Links ?? ProfileLinks.None;
                return new PendingProfileRequest(
                    request.Id,
                    request.Kind,
                    request.RequesterUserId,
                    draft?.Type ?? profile?.Type ?? string.Empty,
                    draft?.Name ?? profile?.Name ?? string.Empty,
                    draft?.City ?? profile?.City ?? string.Empty,
                    draft?.Styles ?? profile?.Styles ?? [],
                    new ProfileLinksResponse(links.Instagram, links.Bandcamp, links.MetalArchives),
                    request.ContactOrProof,
                    request.CreatedAtUtc,
                    request.ProfileId);
            }),
        ];
    }

    /// <summary>Os perfis citados pelos pedidos (o reivindicado ou o criado na aprovação), por id.</summary>
    private async Task<Dictionary<Guid, Profile>> ProfilesByIdAsync(IEnumerable<ProfileRequest> requests, CancellationToken cancellationToken)
    {
        var ids = requests.Where(request => request.ProfileId is not null).Select(request => request.ProfileId!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, Profile>();
        }

        return await db.Profiles.AsNoTracking()
            .Where(profile => ids.Contains(profile.Id))
            .ToDictionaryAsync(profile => profile.Id, cancellationToken);
    }

    /// <summary>Tipo e nome do perfil do pedido: os dados pedidos (novo) ou os do perfil reivindicado.</summary>
    internal static (string Type, string Name) Describe(ProfileRequest request, IReadOnlyDictionary<Guid, Profile> profiles)
    {
        if (request.Draft is { } draft)
        {
            return (draft.Type, draft.Name);
        }

        return request.ProfileId is { } id && profiles.TryGetValue(id, out var profile)
            ? (profile.Type, profile.Name)
            : (string.Empty, string.Empty);
    }

    private static string EscapeLike(string term) =>
        term.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
