namespace Subsolo.Modules.Profiles.Domain.Profiles;

/// <summary>
/// SPEC-0021 — links do perfil, já validados (<c>https</c> dos domínios oficiais do Instagram, do Bandcamp e do Metal
/// Archives); <see langword="null"/> = não informado.
/// </summary>
public sealed record ProfileLinks(string? Instagram, string? Bandcamp, string? MetalArchives)
{
    /// <summary>Nenhum link.</summary>
    public static readonly ProfileLinks None = new(null, null, null);
}
