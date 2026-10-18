using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Domain.Cities;
using Subsolo.Modules.Events.Domain.Shows;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Events.Application.Features.CreateShow;

/// <summary>
/// SPEC-0023 — do pedido (já validado) aos dados do show, no cadastro e na edição: confere que o usuário é dono do
/// organizador (salvo o admin, que pode em qualquer perfil, inclusive sem dono — ADR-0003), que a casa com perfil é um
/// perfil de casa e as bandas com perfil são perfis de banda (pelo <see cref="IShowProfiles"/>, só aprovados; o nome vem
/// do perfil), converte o início local da cidade para UTC e confere o flyer.
/// </summary>
internal sealed class ShowInputResolver(IShowProfiles profiles, ICityRepository cities, IFlyerCatalog flyers)
{
    private const string VenueType = "venue";

    private const string BandType = "band";

    /// <summary>O usuário pode mexer em show do organizador <paramref name="organizerProfileId"/>.</summary>
    public static async Task<Result> AuthorizeAsync(
        IShowProfiles profiles, Guid organizerProfileId, Guid userId, bool isAdmin, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        if (isAdmin)
        {
            return Result.Success();
        }

        var found = await profiles.GetManyAsync([organizerProfileId], ct);
        return found.TryGetValue(organizerProfileId, out var organizer) && organizer.OwnerUserId == userId
            ? Result.Success()
            : Result.Failure(EventsErrors.ShowForbiddenError());
    }

    /// <summary>Os dados do show a partir de <paramref name="input"/>, ou o erro do campo.</summary>
    public async Task<Result<ShowData>> ResolveAsync(IShowFields input, bool flyerChanged, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var authorized = await AuthorizeAsync(profiles, input.OrganizerProfileId, input.UserId, input.IsAdmin, ct);
        if (authorized.IsFailure)
        {
            return Result.Failure<ShowData>(authorized.Error);
        }

        var venueInput = input.Venue!;
        var lineupInput = input.Lineup!;
        Guid[] wanted =
        [
            input.OrganizerProfileId,
            .. venueInput.ProfileId is { } venueId ? [venueId] : Array.Empty<Guid>(),
            .. lineupInput.Where(item => item.ProfileId is not null).Select(item => item.ProfileId!.Value),
        ];
        var found = await profiles.GetManyAsync([.. wanted.Distinct()], ct);
        if (!found.ContainsKey(input.OrganizerProfileId))
        {
            return Invalid(nameof(IShowFields.OrganizerProfileId), "Perfil organizador não encontrado.");
        }

        ShowVenue venue;
        if (venueInput.ProfileId is { } venueProfileId)
        {
            if (!found.TryGetValue(venueProfileId, out var venueProfile) || venueProfile.Type != VenueType)
            {
                return Invalid(nameof(IShowFields.Venue), "Casa não encontrada. Escolha outra ou escreva o nome.");
            }

            venue = new ShowVenue(venueProfile.Id, venueProfile.Name);
        }
        else
        {
            venue = new ShowVenue(null, venueInput.Name!.Trim());
        }

        var lineup = new List<ShowLineupItem>(lineupInput.Count);
        for (var index = 0; index < lineupInput.Count; index++)
        {
            var item = lineupInput[index];
            if (item.ProfileId is { } bandId)
            {
                if (!found.TryGetValue(bandId, out var band) || band.Type != BandType)
                {
                    return Invalid($"{nameof(IShowFields.Lineup)}[{index}]", "Banda não encontrada. Escolha outra ou escreva o nome.");
                }

                lineup.Add(new ShowLineupItem(band.Id, band.Name));
            }
            else
            {
                lineup.Add(new ShowLineupItem(null, item.Name!.Trim()));
            }
        }

        var city = await cities.GetBySlugAsync(input.City!, ct);
        if (city is null)
        {
            return Invalid(nameof(IShowFields.City), "Escolha uma cidade da lista.");
        }

        if (ShowLocalTime.TryToUtc(input.StartsLocal, city.TimeZone) is not { } startsAtUtc)
        {
            return Invalid(nameof(IShowFields.StartsLocal), "Esse horário não existe nessa data.");
        }

        var flyerId = input.FlyerId!;
        if (flyerChanged && !await flyers.ExistsAsync(flyerId, ct))
        {
            return Result.Failure<ShowData>(EventsErrors.FlyerNotFoundError());
        }

        return Result.Success(new ShowData(
            input.OrganizerProfileId,
            input.Title!.Trim(),
            city.Slug,
            startsAtUtc,
            venue,
            lineup,
            [.. input.Styles!.Distinct(StringComparer.Ordinal)],
            input.Price!.Trim(),
            string.IsNullOrWhiteSpace(input.TicketUrl) ? null : input.TicketUrl.Trim(),
            flyerId));
    }

    /// <summary>O slug do show: <c>&lt;titulo&gt;-&lt;aaaa-mm-dd&gt;</c>, com a data local da cidade.</summary>
    public static string SlugOf(string title, DateTimeOffset startsAtUtc, string timeZoneId) =>
        $"{Domain.Slugs.SlugGenerator.FromName(title)}-{ShowLocalTime.ToLocal(startsAtUtc, timeZoneId):yyyy-MM-dd}";

    private static Result<ShowData> Invalid(string field, string message) =>
        Result.Failure<ShowData>(EventsErrors.InvalidShowError(field, message));
}
