using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Subsolo.Users.UnitTests.Infrastructure;

/// <summary>
/// SPEC-0014 (Emenda v2) — o token de sessão do cookie: 256 bits aleatórios em base64url (43 caracteres, sem
/// preenchimento); no banco, só o hash SHA-256 dele. A codificação do hash gravado é escolha da implementação: vale o
/// SHA-256 do texto do token ou dos 32 bytes dele, em hex, base64 ou base64url.
/// </summary>
internal static class SessionTokens
{
    /// <summary>Um token novo no formato do contrato (32 bytes aleatórios em base64url).</summary>
    public static string NewRandom() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>O token tem 43 caracteres base64url e decodifica em exatamente 32 bytes (256 bits).</summary>
    public static void ShouldBe256BitBase64Url(string token)
    {
        token.Length.ShouldBe(43, $"token de sessão \"{token}\" não tem 43 caracteres base64url (256 bits)");
        Base64Url.IsValid(token).ShouldBeTrue($"token de sessão \"{token}\" não é base64url");
        Base64Url.DecodeFromChars(token).Length.ShouldBe(32, "o token de sessão não tem 256 bits");
    }

    /// <summary>O valor gravado é o hash SHA-256 do token (em uma das codificações aceitas), e não o token.</summary>
    public static void ShouldBeHashOf(string stored, string token)
    {
        stored.ShouldNotContain(token, Case.Sensitive, "o token da sessão foi gravado em claro");
        var normalized = stored.StartsWith("\\x", StringComparison.Ordinal) ? stored[2..] : stored;
        Candidates(token).ShouldContain(
            candidate => string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.TrimEnd('='), normalized.TrimEnd('='), StringComparison.Ordinal),
            $"o valor gravado \"{stored}\" não é o SHA-256 do token");
    }

    private static IEnumerable<string> Candidates(string token)
    {
        foreach (var hash in new[] { SHA256.HashData(Encoding.UTF8.GetBytes(token)), SHA256.HashData(Base64Url.DecodeFromChars(token)) })
        {
            yield return Convert.ToHexString(hash);
            yield return Convert.ToBase64String(hash);
            yield return Base64Url.EncodeToString(hash);
        }
    }
}
