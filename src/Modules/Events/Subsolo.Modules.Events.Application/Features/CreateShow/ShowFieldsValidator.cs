using FluentValidation;
using Subsolo.Modules.Events.Domain.Cities;
using Subsolo.Modules.Events.Domain.Flyers;
using Subsolo.Modules.Events.Domain.Shows;
using Subsolo.Modules.Events.Domain.Slugs;
using Subsolo.Modules.Events.Domain.Styles;

namespace Subsolo.Modules.Events.Application.Features.CreateShow;

/// <summary>
/// SPEC-0023 — as regras dos campos do show, iguais no cadastro e na edição: título 2–100 (com letras ou dígitos para o
/// slug), cidade existente, início local no formato <c>aaaa-mm-ddThh:mm</c>, existente no fuso da cidade e no futuro,
/// casa com perfil ou nome 2–80, line-up 1–15 (perfil ou nome 1–80), estilos existentes (1–5), preço 1–40, link do
/// ingresso só <c>https</c> e o flyer informado. Cada falha leva <see cref="EventsErrors.InvalidShow"/> no
/// <c>ErrorCode</c>, ligada ao campo.
/// </summary>
internal abstract class ShowFieldsValidator<T> : AbstractValidator<T>
    where T : IShowFields
{
    protected ShowFieldsValidator(ICityRepository cities, IStyleRepository styles, TimeProvider clock)
    {
        RuleFor(show => show.Title)
            .Must(title => title?.Trim().Length is >= Show.TitleMinLength and <= Show.TitleMaxLength)
            .WithMessage($"O nome deve ter de {Show.TitleMinLength} a {Show.TitleMaxLength} caracteres.")
            .WithErrorCode(EventsErrors.InvalidShow)
            .DependentRules(() => RuleFor(show => show.Title)
                .Must(title => SlugGenerator.FromName(title!).Length > 0)
                .WithMessage("O nome precisa ter ao menos uma letra ou um dígito.")
                .WithErrorCode(EventsErrors.InvalidShow));

        RuleFor(show => show.City)
            .MustAsync(async (city, ct) => !string.IsNullOrWhiteSpace(city) && await cities.GetBySlugAsync(city, ct) is not null)
            .WithMessage("Escolha uma cidade da lista.")
            .WithErrorCode(EventsErrors.InvalidShow);

        RuleFor(show => show.StartsLocal)
            .CustomAsync(async (startsLocal, context, ct) =>
            {
                if (ShowLocalTime.Parse(startsLocal) is not { } local)
                {
                    context.AddFailure(Failure(nameof(IShowFields.StartsLocal), "Informe a data e a hora (aaaa-mm-ddThh:mm)."));
                    return;
                }

                var city = string.IsNullOrWhiteSpace(context.InstanceToValidate.City)
                    ? null
                    : await cities.GetBySlugAsync(context.InstanceToValidate.City, ct);
                if (city is null)
                {
                    return;
                }

                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(city.TimeZone);
                if (timeZone.IsInvalidTime(local))
                {
                    context.AddFailure(Failure(nameof(IShowFields.StartsLocal), "Esse horário não existe nessa data (mudança de horário de verão)."));
                    return;
                }

                if (ShowLocalTime.ToUtc(local, city.TimeZone) <= clock.GetUtcNow())
                {
                    context.AddFailure(Failure(nameof(IShowFields.StartsLocal), "A data e a hora do show já passaram."));
                }
            });

        RuleFor(show => show.Venue)
            .Must(venue => venue is not null && (venue.ProfileId is not null || IsVenueName(venue.Name)))
            .WithMessage($"Escolha a casa ou escreva o nome (de {Show.VenueNameMinLength} a {Show.NameMaxLength} caracteres).")
            .WithErrorCode(EventsErrors.InvalidShow);

        RuleFor(show => show.Lineup)
            .Must(lineup => lineup?.Count is >= Show.LineupMin and <= Show.LineupMax)
            .WithMessage($"O line-up deve ter de {Show.LineupMin} a {Show.LineupMax} bandas.")
            .WithErrorCode(EventsErrors.InvalidShow);

        RuleForEach(show => show.Lineup)
            .Must(item => item is not null && (item.ProfileId is not null || IsBandName(item.Name)))
            .WithMessage($"Escolha a banda ou escreva o nome (até {Show.NameMaxLength} caracteres).")
            .WithErrorCode(EventsErrors.InvalidShow)
            .When(show => show.Lineup is not null);

        RuleFor(show => show.Styles)
            .Must(list => list?.Count is >= Show.StylesMin and <= Show.StylesMax)
            .WithMessage($"Escolha de {Show.StylesMin} a {Show.StylesMax} estilos.")
            .WithErrorCode(EventsErrors.InvalidShow)
            .DependentRules(() => RuleFor(show => show.Styles)
                .MustAsync(async (list, ct) =>
                {
                    foreach (var slug in list!.Distinct(StringComparer.Ordinal))
                    {
                        if (string.IsNullOrWhiteSpace(slug) || await styles.GetBySlugAsync(slug, ct) is null)
                        {
                            return false;
                        }
                    }

                    return true;
                })
                .WithMessage("Escolha estilos da lista.")
                .WithErrorCode(EventsErrors.InvalidShow));

        RuleFor(show => show.Price)
            .Must(price => price?.Trim().Length is >= 1 and <= Show.PriceMaxLength)
            .WithMessage($"Informe o preço (até {Show.PriceMaxLength} caracteres, ex.: \"Gratuito\").")
            .WithErrorCode(EventsErrors.InvalidShow);

        RuleFor(show => show.TicketUrl)
            .Must(IsHttps)
            .When(show => !string.IsNullOrWhiteSpace(show.TicketUrl))
            .WithMessage("O link do ingresso precisa começar com https://.")
            .WithErrorCode(EventsErrors.InvalidShow);

        RuleFor(show => show.FlyerId)
            .Must(flyerId => flyerId?.Length == Flyer.IdLength)
            .WithMessage("Envie o flyer do show.")
            .WithErrorCode(EventsErrors.InvalidShow);
    }

    /// <summary>Link absoluto <c>https</c> com host (até 2000 caracteres).</summary>
    internal static bool IsHttps(string? url) =>
        url is { Length: <= 2000 }
        && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrEmpty(uri.Host);

    private static bool IsVenueName(string? name) =>
        name?.Trim().Length is >= Show.VenueNameMinLength and <= Show.NameMaxLength;

    private static bool IsBandName(string? name) =>
        name?.Trim().Length is >= 1 and <= Show.NameMaxLength;

    private static FluentValidation.Results.ValidationFailure Failure(string field, string message) =>
        new(field, message) { ErrorCode = EventsErrors.InvalidShow };
}
