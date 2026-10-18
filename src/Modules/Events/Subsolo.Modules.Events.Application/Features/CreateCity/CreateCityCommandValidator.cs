using FluentValidation;

namespace Subsolo.Modules.Events.Application.Features.CreateCity;

/// <summary>
/// SPEC-0017 — nome com 2 a 60 caracteres, UF entre as 27, fuso IANA válido e slug (quando informado) no formato
/// <c>^[a-z0-9-]{2,60}$</c> → <c>events.invalid_city</c>; slug reservado → <c>events.city_slug_reserved</c> (o código
/// vai no <c>ErrorCode</c> de cada falha).
/// </summary>
internal sealed class CreateCityCommandValidator : AbstractValidator<CreateCityCommand>
{
    public CreateCityCommandValidator()
    {
        RuleFor(command => command.Name).CityName();
        RuleFor(command => command.State).State();
        RuleFor(command => command.TimeZone).TimeZone();
        RuleFor(command => command.Slug).CitySlug().When(command => command.Slug is not null);
    }
}
