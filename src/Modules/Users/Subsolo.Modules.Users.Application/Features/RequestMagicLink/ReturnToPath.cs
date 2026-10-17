namespace Subsolo.Modules.Users.Application.Features.RequestMagicLink;

/// <summary>
/// SPEC-0016 — saneamento do <c>returnTo</c>: só caminho relativo do próprio site (<c>/</c> seguido de algo que não
/// seja <c>/</c> nem <c>\</c>); qualquer outra coisa (vazio, nulo, URL absoluta, <c>//host</c>, <c>/\host</c>,
/// <c>javascript:</c>) vira <c>/</c>, para não haver redirecionamento aberto.
/// </summary>
/// <remarks>Scaffold do Test-writer: sem lógica (a implementação é do Green).</remarks>
internal static class ReturnToPath
{
    /// <summary>Caminho usado quando o <c>returnTo</c> falta ou não é seguro.</summary>
    public const string Default = "/";

    public static string Sanitize(string? returnTo) =>
        throw new NotImplementedException("SPEC-0016: ReturnToPath.Sanitize");
}
