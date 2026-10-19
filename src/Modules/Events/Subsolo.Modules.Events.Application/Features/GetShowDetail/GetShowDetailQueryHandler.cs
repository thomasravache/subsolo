using Subsolo.Common.Application.Media;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;
using Subsolo.Modules.Events.Application.Features.GetShow;
using Subsolo.Modules.Events.Domain.Cities;
using Subsolo.Modules.Events.Domain.Flyers;
using Subsolo.Modules.Events.Domain.Styles;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Events.Application.Features.GetShowDetail;

/// <summary>
/// SPEC-0024 — cidade inexistente → <c>events.city_not_found</c>; show inexistente na cidade → <c>events.show_not_found</c>
/// (cancelado e adiado continuam com página, com a situação). Os slugs dos perfis vêm do <see cref="IShowProfiles"/>
/// (só os aprovados) e os nomes dos estilos do <see cref="IStyleRepository"/>.
/// </summary>
internal sealed class GetShowDetailQueryHandler(
    ICityRepository cities,
    IShowsReadModel shows,
    IStyleRepository styles,
    IShowProfiles profiles)
    : IQueryHandler<GetShowDetailQuery, ShowDetailResponse>
{
    public async Task<Result<ShowDetailResponse>> Handle(GetShowDetailQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var city = await cities.GetBySlugAsync(query.City, cancellationToken);
        if (city is null)
        {
            return Result.Failure<ShowDetailResponse>(EventsErrors.CityNotFoundError(query.City));
        }

        var found = await shows.GetBySlugAsync(city.Slug, query.Slug, cancellationToken);
        if (found is null)
        {
            return Result.Failure<ShowDetailResponse>(EventsErrors.ShowNotFoundError());
        }

        var show = found.Show;
        var profileIds = new[] { show.Venue }
            .Concat(show.Lineup)
            .Where(part => part.ProfileId is not null)
            .Select(part => part.ProfileId!.Value)
            .Distinct()
            .ToList();
        var linked = profileIds.Count == 0
            ? new Dictionary<Guid, ShowProfile>()
            : await profiles.GetManyAsync(profileIds, cancellationToken);
        var styleNames = (await styles.ListAsync(cancellationToken)).ToDictionary(style => style.Slug, style => style.Name, StringComparer.Ordinal);

        ShowDetailPartResponse Part(ShowPartResponse part) => new(
            part.Name,
            part.ProfileId is { } id && linked.TryGetValue(id, out var profile) ? profile.Slug : null);

        return Result.Success(new ShowDetailResponse(
            show.Id,
            show.Slug,
            show.Title,
            new ShowDetailCityResponse(city.Slug, city.Name, city.TimeZone),
            show.StartsAtUtc.UtcDateTime,
            Part(show.Venue),
            [.. show.Lineup.Select(Part)],
            [.. show.Styles.Select(slug => new ShowDetailStyleResponse(slug, styleNames.GetValueOrDefault(slug, slug)))],
            show.Price,
            show.TicketUrl,
            ShowStatusMapping.ToResponse(show.Status),
            new ShowDetailFlyerResponse(
                Url(show.FlyerId, Flyer.Thumb),
                Url(show.FlyerId, Flyer.Full),
                Url(show.FlyerId, Flyer.Og),
                found.FlyerWidth,
                found.FlyerHeight)));
    }

    private static string Url(string flyerId, string file) => MediaPaths.PublicUrl(Flyer.VariantPath(flyerId, file));
}
