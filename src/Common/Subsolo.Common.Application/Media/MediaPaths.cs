namespace Subsolo.Common.Application.Media;

/// <summary>
/// SPEC-0022 (ADR-0015) — onde a mídia fica: caminhos relativos ao <c>Media:Root</c> (os do <see cref="IFileStorage"/>)
/// e o prefixo público de onde ela é servida (<c>/media</c>: pelo Caddy no Compose e em produção, pela API no
/// desenvolvimento).
/// </summary>
public static class MediaPaths
{
    /// <summary>Prefixo público: <c>/media/&lt;caminho relativo&gt;</c>.</summary>
    public const string PublicPrefix = "/media";

    /// <summary>Diretório dos flyers, relativo ao <c>Media:Root</c>.</summary>
    public const string Flyers = "flyers";

    /// <summary>Cache das variantes: o nome é o hash do conteúdo, então o arquivo nunca muda.</summary>
    public const string ImmutableCacheControl = "public, max-age=31536000, immutable";

    /// <summary>A URL pública de <paramref name="relativePath"/>.</summary>
    public static string PublicUrl(string relativePath) => $"{PublicPrefix}/{relativePath}";
}
