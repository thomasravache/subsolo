using System.Collections.Frozen;
using FluentValidation;
using Subsolo.Modules.Events.Domain.Cities;
using Subsolo.Modules.Events.Domain.Slugs;

namespace Subsolo.Modules.Events.Application.Features.CreateCity;

/// <summary>
/// SPEC-0017 — regras dos campos de uma cidade, comuns a criar e editar: nome com 2 a 60 caracteres, UF entre as 27,
/// fuso IANA válido e slug no formato e fora dos reservados.
/// </summary>
internal static class CityRules
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 60;

    /// <summary>As 27 UFs.</summary>
    public static readonly FrozenSet<string> States = new[]
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ",
        "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    }.ToFrozenSet(StringComparer.Ordinal);

    public static IRuleBuilderOptions<T, string> CityName<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(name => name?.Trim().Length is >= NameMinLength and <= NameMaxLength)
            .WithErrorCode(EventsErrors.InvalidCity)
            .WithMessage($"O nome deve ter de {NameMinLength} a {NameMaxLength} caracteres.");

    public static IRuleBuilderOptions<T, string> State<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(state => state is not null && States.Contains(state))
            .WithErrorCode(EventsErrors.InvalidCity)
            .WithMessage("A UF deve ser uma das 27 siglas (ex.: SP).");

    public static IRuleBuilderOptions<T, string> TimeZone<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(IsIanaTimeZone)
            .WithErrorCode(EventsErrors.InvalidCity)
            .WithMessage("O fuso deve ser um fuso horário IANA válido (ex.: America/Sao_Paulo).");

    /// <summary>Slug informado: reservado → <c>events.city_slug_reserved</c>; fora do formato → <c>events.invalid_city</c>.</summary>
    public static IRuleBuilderOptions<T, string?> CitySlug<T>(this IRuleBuilderInitial<T, string?> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(slug => slug is null || !City.IsReservedSlug(slug))
            .WithErrorCode(EventsErrors.CitySlugReserved)
            .WithMessage(ReservedSlugMessage)
            .Matches(SlugGenerator.Pattern)
            .WithErrorCode(EventsErrors.InvalidCity)
            .WithMessage(SlugPatternMessage);

    public const string ReservedSlugMessage = "Esse endereço é reservado.";

    public const string SlugPatternMessage = "O endereço deve ter de 2 a 60 letras minúsculas, dígitos ou hífens.";

    /// <summary>Fuso IANA conhecido do sistema (os ids do Windows não valem).</summary>
    private static bool IsIanaTimeZone(string? timeZone) =>
        !string.IsNullOrWhiteSpace(timeZone)
        && timeZone.Length <= 64
        && TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var found)
        && found.HasIanaId;
}
