using Subsolo.Modules.Users.Domain.MagicLinks;

namespace Subsolo.Modules.Users.Application.Features.RequestMagicLink;

/// <summary>
/// SPEC-0016 — saneamento do <c>returnTo</c>: só caminho relativo do próprio site (<c>/</c> seguido de algo que não
/// seja <c>/</c> nem <c>\</c>); qualquer outra coisa (vazio, nulo, URL absoluta, <c>//host</c>, <c>/\host</c>,
/// <c>javascript:</c>) vira <c>/</c>, para não haver redirecionamento aberto.
/// </summary>
internal static class ReturnToPath
{
    /// <summary>Caminho usado quando o <c>returnTo</c> falta ou não é seguro.</summary>
    public const string Default = "/";

    /// <summary>
    /// <paramref name="returnTo"/> quando é um caminho relativo seguro (começa com uma <c>/</c> só, sem <c>\</c>, sem
    /// caracteres de controle e com até <see cref="MagicLinkToken.ReturnToMaxLength"/> caracteres); senão,
    /// <see cref="Default"/>.
    /// </summary>
    public static string Sanitize(string? returnTo)
    {
        if (string.IsNullOrEmpty(returnTo)
            || returnTo.Length > MagicLinkToken.ReturnToMaxLength
            || returnTo[0] != '/'
            || returnTo.StartsWith("//", StringComparison.Ordinal)
            || returnTo.Contains('\\', StringComparison.Ordinal)
            || returnTo.Any(char.IsControl))
        {
            return Default;
        }

        return returnTo;
    }
}
