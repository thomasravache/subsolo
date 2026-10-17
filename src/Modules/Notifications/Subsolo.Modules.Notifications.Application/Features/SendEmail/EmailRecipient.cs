using System.Text.RegularExpressions;

namespace Subsolo.Modules.Notifications.Application.Features.SendEmail;

/// <summary>
/// SPEC-0018 — destinatário do e-mail: validação (formato, até 254 caracteres, sem espaço nem caractere de controle,
/// o que barra a injeção de cabeçalho com <c>\r\n</c>) e a forma mascarada para logs (<c>f***@subsolo.test</c>, ADR-0008).
/// </summary>
internal static partial class EmailRecipient
{
    /// <summary>Tamanho máximo de um endereço (RFC 5321, caminho de 256 menos os sinais de maior e menor).</summary>
    public const int MaxLength = 254;

    /// <summary>O endereço é aceito como destinatário (sem normalizar: o que chega é o que vai para o SMTP).</summary>
    public static bool IsValid(string? to) =>
        to is { Length: > 0 and <= MaxLength } && !to.Any(char.IsControl) && Format().IsMatch(to);

    /// <summary>Primeira letra da parte local, <c>***</c> e o domínio: nunca o endereço em claro.</summary>
    public static string Mask(string to)
    {
        var at = to.LastIndexOf('@');
        return at > 0 ? $"{to[0]}***{to[at..]}" : "***";
    }

    /// <summary>Uma parte local e um domínio com ponto, sem espaços nem um segundo <c>@</c>.</summary>
    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Format();
}
