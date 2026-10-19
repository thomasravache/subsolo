using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Profiles.Domain.Profiles;

namespace Subsolo.Modules.Profiles.Application.Features.GetPublicProfile;

/// <summary>
/// SPEC-0026 — lê o perfil aprovado (todo perfil gravado já é aprovado: pedidos pendentes e recusados não viram perfil)
/// e completa a cidade e os estilos com os nomes do Events (<see cref="IEventsCatalogNames"/>, ADR-0001).
/// </summary>
internal sealed class GetPublicProfileQueryHandler(IProfilesReadModel profiles, IEventsCatalogNames catalog)
    : IQueryHandler<GetPublicProfileQuery, PublicProfileResponse>
{
    public async Task<Result<PublicProfileResponse>> Handle(GetPublicProfileQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!ProfileTypes.All.Contains(query.Type, StringComparer.Ordinal) || string.IsNullOrWhiteSpace(query.Slug))
        {
            return Result.Failure<PublicProfileResponse>(ProfilesErrors.NotFoundError());
        }

        var profile = await profiles.GetPublicAsync(query.Type, query.Slug, cancellationToken);
        if (profile is null)
        {
            return Result.Failure<PublicProfileResponse>(ProfilesErrors.NotFoundError());
        }

        var names = await catalog.GetNamesAsync([profile.City], profile.Styles, cancellationToken);
        return Result.Success(new PublicProfileResponse(
            profile.Id,
            profile.Type,
            profile.Name,
            profile.Slug,
            new ProfileCityResponse(profile.City, names.Cities.GetValueOrDefault(profile.City, profile.City)),
            [.. profile.Styles.Select(style => new ProfileStyleResponse(style, names.Styles.GetValueOrDefault(style, style)))],
            new PublicProfileLinksResponse
            {
                Instagram = profile.Instagram,
                Bandcamp = profile.Bandcamp,
                MetalArchives = profile.MetalArchives,
            }));
    }
}
