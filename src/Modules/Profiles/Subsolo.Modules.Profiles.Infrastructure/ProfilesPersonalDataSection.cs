using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Profiles.Infrastructure;

/// <summary>
/// SPEC-0021 — seção <see cref="SectionName"/> do export de dados pessoais (SPEC-0019): os perfis de que o titular é
/// dono e os pedidos dele (com o contato ou a prova que ele mesmo informou); sem nada, a seção é omitida.
/// </summary>
internal sealed class ProfilesPersonalDataSection(ProfilesDbContext db) : IPersonalDataSection
{
    public const string SectionName = "profiles";

    public string Name => SectionName;

    public async Task<object?> ExportAsync(Guid userId, CancellationToken ct)
    {
        var profiles = await db.Profiles.AsNoTracking()
            .Where(profile => profile.OwnerUserId == userId)
            .OrderBy(profile => profile.CreatedAtUtc)
            .ToListAsync(ct);
        var requests = await db.ProfileRequests.AsNoTracking()
            .Where(request => request.RequesterUserId == userId)
            .OrderBy(request => request.CreatedAtUtc)
            .ToListAsync(ct);
        if (profiles.Count == 0 && requests.Count == 0)
        {
            return null;
        }

        var ids = requests.Where(request => request.ProfileId is not null).Select(request => request.ProfileId!.Value).Distinct().ToList();
        var claimed = await db.Profiles.AsNoTracking()
            .Where(profile => ids.Contains(profile.Id))
            .ToDictionaryAsync(profile => profile.Id, ct);

        return new ProfilesExport(
            [
                .. profiles.Select(profile => new ExportedProfile(
                    profile.Id, profile.Type, profile.Name, profile.Slug, profile.City, profile.Styles, profile.CreatedAtUtc)),
            ],
            [
                .. requests.Select(request =>
                {
                    var (type, name) = ProfilesReadModel.Describe(request, claimed);
                    return new ExportedProfileRequest(
                        request.Id,
                        request.Kind,
                        type,
                        name,
                        request.ProfileId,
                        request.ContactOrProof,
                        request.Status,
                        request.Reason,
                        request.CreatedAtUtc,
                        request.DecidedAtUtc);
                }),
            ]);
    }

    private sealed record ProfilesExport(IReadOnlyList<ExportedProfile> Profiles, IReadOnlyList<ExportedProfileRequest> Requests);

    private sealed record ExportedProfile(
        Guid Id, string Type, string Name, string Slug, string City, IReadOnlyList<string> Styles, DateTimeOffset CreatedAtUtc);

    private sealed record ExportedProfileRequest(
        Guid RequestId,
        string Kind,
        string Type,
        string Name,
        Guid? ProfileId,
        string ContactOrProof,
        string Status,
        string? Reason,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? DecidedAtUtc);
}
