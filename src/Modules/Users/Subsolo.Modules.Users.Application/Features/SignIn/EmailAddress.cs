using System.Text.RegularExpressions;
using Subsolo.Modules.Users.Domain.Users;

namespace Subsolo.Modules.Users.Application.Features.SignIn;

/// <summary>SPEC-0014 — normalização (sem espaços nas pontas, minúsculas) e validação do e-mail de entrada.</summary>
internal static partial class EmailAddress
{
    /// <summary>O e-mail normalizado, ou <see langword="null"/> quando fora do formato ou com mais de 254 caracteres.</summary>
    public static string? Normalize(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant();
        return normalized is { Length: > 0 and <= User.EmailMaxLength } && Format().IsMatch(normalized)
            ? normalized
            : null;
    }

    /// <summary>Uma parte local e um domínio com ponto, sem espaços nem um segundo <c>@</c>.</summary>
    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Format();
}
