namespace Subsolo.Modules.Events.Domain.Slugs;

/// <summary>
/// SPEC-0017 — endereço (slug) gerado do nome de uma cidade ou estilo: sem acento, minúsculas, espaços → hífen, no
/// formato <see cref="Pattern"/> (ex.: "São José dos Pinhais" → <c>sao-jose-dos-pinhais</c>).
/// </summary>
public static class SlugGenerator
{
    /// <summary>Formato do slug na URL.</summary>
    public const string Pattern = "^[a-z0-9-]{2,60}$";

    /// <summary>O slug gerado de <paramref name="name"/>.</summary>
    public static string FromName(string name) =>
        throw new NotImplementedException("SPEC-0017: gerador de slug ainda não implementado.");
}
