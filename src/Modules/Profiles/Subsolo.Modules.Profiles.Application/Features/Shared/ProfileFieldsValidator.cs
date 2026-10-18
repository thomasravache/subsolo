using FluentValidation;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Profiles.Domain.Profiles;
using Subsolo.Modules.Profiles.Domain.Slugs;

namespace Subsolo.Modules.Profiles.Application.Features.Shared;

/// <summary>
/// SPEC-0021 — regras comuns dos campos de um perfil: tipo conhecido, nome 2–80 (sem contar os espaços nas pontas, com
/// ao menos uma letra ou dígito para o slug), cidade e estilos que o <see cref="IEventsCatalog"/> confirma, estilos só
/// em banda (1–5) e links só <c>https</c> do domínio oficial de cada um. Toda falha leva
/// <see cref="ProfilesErrors.InvalidRequest"/> no <c>ErrorCode</c>, ligada ao campo.
/// </summary>
internal abstract class ProfileFieldsValidator<T> : AbstractValidator<T>
    where T : IProfileFields
{
    /// <summary>Quantidade mínima de estilos numa banda.</summary>
    public const int MinStyles = 1;

    /// <summary>Quantidade máxima de estilos numa banda.</summary>
    public const int MaxStyles = 5;

    protected ProfileFieldsValidator(IEventsCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        RuleFor(fields => fields.Type)
            .Must(type => type is not null && ProfileTypes.All.Contains(type, StringComparer.Ordinal))
            .WithMessage("Escolha o tipo: banda, casa ou produtor.")
            .WithErrorCode(ProfilesErrors.InvalidRequest);

        RuleFor(fields => fields.Name)
            .Must(BeAValidName)
            .WithMessage($"O nome precisa ter de {Profile.NameMinLength} a {Profile.NameMaxLength} caracteres, com letras ou dígitos.")
            .WithErrorCode(ProfilesErrors.InvalidRequest);

        RuleFor(fields => fields.City)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Escolha a cidade.")
            .WithErrorCode(ProfilesErrors.InvalidRequest)
            .MustAsync((city, ct) => catalog.CityExistsAsync(city!.Trim(), ct))
            .WithMessage("Cidade não encontrada.")
            .WithErrorCode(ProfilesErrors.InvalidRequest);

        When(fields => fields.Type == ProfileTypes.Band, () =>
            RuleFor(fields => fields.Styles)
                .Cascade(CascadeMode.Stop)
                .Must(styles => styles is { Count: >= MinStyles and <= MaxStyles } && styles.All(style => !string.IsNullOrWhiteSpace(style)))
                .WithMessage($"Escolha de {MinStyles} a {MaxStyles} estilos.")
                .WithErrorCode(ProfilesErrors.InvalidRequest)
                .MustAsync((styles, ct) => catalog.StylesExistAsync([.. styles!.Select(style => style.Trim()).Distinct(StringComparer.Ordinal)], ct))
                .WithMessage("Estilo não encontrado.")
                .WithErrorCode(ProfilesErrors.InvalidRequest));

        When(fields => fields.Type is ProfileTypes.Venue or ProfileTypes.Producer, () =>
            RuleFor(fields => fields.Styles)
                .Must(styles => styles is null || styles.Count == 0)
                .WithMessage("Só banda tem estilos.")
                .WithErrorCode(ProfilesErrors.InvalidRequest));

        RuleFor(fields => fields.Links!.Instagram)
            .Must(ProfileLinkRules.IsInstagram)
            .When(fields => !string.IsNullOrWhiteSpace(fields.Links?.Instagram))
            .WithMessage("Use o endereço https do Instagram (instagram.com).")
            .WithErrorCode(ProfilesErrors.InvalidRequest);

        RuleFor(fields => fields.Links!.Bandcamp)
            .Must(ProfileLinkRules.IsBandcamp)
            .When(fields => !string.IsNullOrWhiteSpace(fields.Links?.Bandcamp))
            .WithMessage("Use o endereço https do Bandcamp (seu-nome.bandcamp.com).")
            .WithErrorCode(ProfilesErrors.InvalidRequest);

        RuleFor(fields => fields.Links!.MetalArchives)
            .Must(ProfileLinkRules.IsMetalArchives)
            .When(fields => !string.IsNullOrWhiteSpace(fields.Links?.MetalArchives))
            .WithMessage("Use o endereço https do Metal Archives (metal-archives.com).")
            .WithErrorCode(ProfilesErrors.InvalidRequest);
    }

    private static bool BeAValidName(string? name)
    {
        var trimmed = name?.Trim();
        return trimmed is { Length: >= Profile.NameMinLength and <= Profile.NameMaxLength }
            && ProfileSlug.FromName(trimmed).Length > 0;
    }
}

/// <summary>SPEC-0021 — links aceitos: <c>https</c>, sem usuário nem porta, no domínio oficial de cada serviço.</summary>
internal static class ProfileLinkRules
{
    public static bool IsInstagram(string? link) =>
        IsHttps(link, out var host) && host is "instagram.com" or "www.instagram.com";

    public static bool IsBandcamp(string? link) =>
        IsHttps(link, out var host) && (host == "bandcamp.com" || host.EndsWith(".bandcamp.com", StringComparison.Ordinal));

    public static bool IsMetalArchives(string? link) =>
        IsHttps(link, out var host) && host is "metal-archives.com" or "www.metal-archives.com";

    private static bool IsHttps(string? link, out string host)
    {
        host = string.Empty;
        if (link is null || !Uri.TryCreate(link.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length > 0 || !uri.IsDefaultPort)
        {
            return false;
        }

        host = uri.IdnHost.ToLowerInvariant();
        return true;
    }
}
