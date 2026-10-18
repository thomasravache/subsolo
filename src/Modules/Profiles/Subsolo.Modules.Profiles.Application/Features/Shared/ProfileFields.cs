using Subsolo.Modules.Profiles.Application.Features.RequestProfile;
using Subsolo.Modules.Profiles.Domain.Profiles;
using Subsolo.Modules.Profiles.Domain.Slugs;

namespace Subsolo.Modules.Profiles.Application.Features.Shared;

/// <summary>
/// SPEC-0021 — os campos de um perfil como chegaram (pedido do fã ou criação pelo admin), validados pelo
/// <see cref="ProfileFieldsValidator{T}"/> com os mesmos nomes de campo.
/// </summary>
internal interface IProfileFields
{
    string? Type { get; }

    string? Name { get; }

    string? City { get; }

    IReadOnlyList<string>? Styles { get; }

    ProfileLinksInput? Links { get; }
}

/// <summary>SPEC-0021 — os campos já validados viram o <see cref="ProfileDraft"/> (nome sem espaços nas pontas, slug gerado).</summary>
internal static class ProfileFields
{
    public static ProfileDraft ToDraft(this IProfileFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var name = fields.Name!.Trim();
        var type = fields.Type!;
        return new ProfileDraft(
            type,
            name,
            ProfileSlug.FromName(name),
            fields.City!.Trim(),
            type == ProfileTypes.Band ? [.. (fields.Styles ?? []).Select(style => style.Trim()).Distinct(StringComparer.Ordinal)] : [],
            new ProfileLinks(
                Clean(fields.Links?.Instagram),
                Clean(fields.Links?.Bandcamp),
                Clean(fields.Links?.MetalArchives)));
    }

    private static string? Clean(string? link) => string.IsNullOrWhiteSpace(link) ? null : link.Trim();
}
