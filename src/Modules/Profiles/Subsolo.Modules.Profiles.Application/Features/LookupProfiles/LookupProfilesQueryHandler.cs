using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Profiles.Application.Features.GetPublicProfile;
using Subsolo.Modules.Profiles.Domain.Profiles;
using Subsolo.Modules.Profiles.Domain.Slugs;

namespace Subsolo.Modules.Profiles.Application.Features.LookupProfiles;

/// <summary>
/// SPEC-0026 (H23-08) — busca pelo nome (sem diferenciar caixa) ou pelo slug (sem acento) entre todos os perfis
/// aprovados; termo com menos de <see cref="MinTermLength"/> caracteres (ou ausente) ou tipo desconhecido →
/// <c>profiles.invalid_query</c>. A cidade vem com o nome do Events (<see cref="IEventsCatalogNames"/>).
/// </summary>
internal sealed class LookupProfilesQueryHandler(IProfilesReadModel profiles, IEventsCatalogNames catalog)
    : IQueryHandler<LookupProfilesQuery, IReadOnlyList<ProfileLookupResponse>>
{
    /// <summary>Tamanho mínimo do termo.</summary>
    public const int MinTermLength = 2;

    /// <summary>Máximo de resultados.</summary>
    public const int Limit = 20;

    public async Task<Result<IReadOnlyList<ProfileLookupResponse>>> Handle(
        LookupProfilesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var term = query.Q?.Trim() ?? string.Empty;
        if (term.Length < MinTermLength)
        {
            return Result.Failure<IReadOnlyList<ProfileLookupResponse>>(
                ProfilesErrors.InvalidQueryError("q", $"Digite ao menos {MinTermLength} caracteres."));
        }

        var type = string.IsNullOrWhiteSpace(query.Type) ? null : query.Type.Trim();
        if (type is not null && !ProfileTypes.All.Contains(type, StringComparer.Ordinal))
        {
            return Result.Failure<IReadOnlyList<ProfileLookupResponse>>(
                ProfilesErrors.InvalidQueryError("type", "Use band, venue ou producer."));
        }

        var found = await profiles.LookupAsync(term, ProfileSlug.FromName(term), type, Limit, cancellationToken);
        if (found.Count == 0)
        {
            return Result.Success<IReadOnlyList<ProfileLookupResponse>>([]);
        }

        var names = await catalog.GetNamesAsync([.. found.Select(profile => profile.City)], [], cancellationToken);
        return Result.Success<IReadOnlyList<ProfileLookupResponse>>(
        [
            .. found.Select(profile => new ProfileLookupResponse(
                profile.Id,
                profile.Type,
                profile.Name,
                profile.Slug,
                new ProfileCityResponse(profile.City, names.Cities.GetValueOrDefault(profile.City, profile.City)))),
        ]);
    }
}
