using Subsolo.Common.Application.Media;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application.Features.CreateShow;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;
using Subsolo.Modules.Events.Domain.Cities;
using Subsolo.Modules.Events.Domain.Flyers;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Events.Application.Features.GetShow;

/// <summary>SPEC-0023 — show inexistente → <c>events.show_not_found</c>; não dono (salvo o admin) → <c>auth.forbidden</c>.</summary>
internal sealed class GetShowQueryHandler(IShowsReadModel shows, ICityRepository cities, IShowProfiles profiles)
    : IQueryHandler<GetShowQuery, ShowForEditResponse>
{
    public async Task<Result<ShowForEditResponse>> Handle(GetShowQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var show = await shows.GetAsync(query.ShowId, cancellationToken);
        if (show is null)
        {
            return Result.Failure<ShowForEditResponse>(EventsErrors.ShowNotFoundError());
        }

        var authorized = await ShowInputResolver.AuthorizeAsync(
            profiles, show.OrganizerProfileId, query.UserId, query.IsAdmin, cancellationToken);
        if (authorized.IsFailure)
        {
            return Result.Failure<ShowForEditResponse>(authorized.Error);
        }

        var city = await cities.GetBySlugAsync(show.City, cancellationToken);
        var organizer = await profiles.GetManyAsync([show.OrganizerProfileId], cancellationToken);
        var local = ShowLocalTime.ToLocal(show.StartsAtUtc, city?.TimeZone ?? "UTC");
        return Result.Success(new ShowForEditResponse(
            show.Id,
            show.Slug,
            show.OrganizerProfileId,
            organizer.TryGetValue(show.OrganizerProfileId, out var profile) ? profile.Name : string.Empty,
            show.Title,
            show.City,
            local.ToString(ShowLocalTime.Format, System.Globalization.CultureInfo.InvariantCulture),
            show.Venue,
            show.Lineup,
            show.Styles,
            show.Price,
            show.TicketUrl,
            show.FlyerId,
            MediaPaths.PublicUrl(Flyer.VariantPath(show.FlyerId, Flyer.Thumb)),
            ShowStatusMapping.ToResponse(show.Status)));
    }
}
