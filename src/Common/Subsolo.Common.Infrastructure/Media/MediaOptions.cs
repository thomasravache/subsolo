namespace Subsolo.Common.Infrastructure.Media;

/// <summary>
/// SPEC-0022 (ADR-0015) — seção <see cref="SectionName"/>: <see cref="Root"/> é o diretório da mídia (no Compose e em
/// produção, o volume <c>media</c>; no <c>aspire run</c>, um diretório local definido pelo AppHost).
/// </summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>Caminho público de onde a mídia é servida (pelo Caddy no Compose, pela API no desenvolvimento).</summary>
    public const string PublicPath = "/media";

    /// <summary>Diretório raiz da mídia (<c>Media:Root</c>).</summary>
    public string? Root { get; set; }
}
