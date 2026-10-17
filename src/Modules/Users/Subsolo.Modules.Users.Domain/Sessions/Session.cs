using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Subsolo.Common.Domain;

namespace Subsolo.Modules.Users.Domain.Sessions;

/// <summary>
/// SPEC-0014 — sessão guardada no banco por trás do cookie <c>__Host-subsolo</c> (ADR-0003): token aleatório de 256 bits
/// (base64url, só no cookie) guardado só como hash SHA-256 (<see cref="IdHash"/>; Emenda v2), prazo deslizante de <see cref="SlidingLifetime"/> renovado no máximo uma vez a cada
/// <see cref="RenewalInterval"/>, teto absoluto de <see cref="AbsoluteLifetime"/> desde a entrada e revogação no servidor.
/// </summary>
public sealed class Session : Entity
{
    /// <summary>Prazo deslizante: sem uso por esse tempo, a sessão expira.</summary>
    public static readonly TimeSpan SlidingLifetime = TimeSpan.FromDays(30);

    /// <summary>Teto absoluto desde a entrada, sem renovação.</summary>
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromDays(90);

    /// <summary>Intervalo mínimo entre duas renovações gravadas.</summary>
    public static readonly TimeSpan RenewalInterval = TimeSpan.FromHours(1);

    /// <summary>Tamanho do token em bytes (256 bits).</summary>
    public const int TokenBytes = 32;

    /// <summary>Tamanho do <see cref="IdHash"/>: SHA-256 em hexadecimal minúsculo.</summary>
    public const int IdHashLength = 64;

    private Session()
    {
    }

    /// <summary>Hash SHA-256 do token da sessão (o token em claro só existe no cookie).</summary>
    public string IdHash { get; private set; } = string.Empty;

    public Guid UserId { get; private set; }

    public SignInMethod Method { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset LastSeenAtUtc { get; private set; }

    /// <summary>Fim do prazo deslizante (nunca além de <see cref="AbsoluteExpiresAtUtc"/>).</summary>
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    /// <summary><see cref="CreatedAtUtc"/> + <see cref="AbsoluteLifetime"/>.</summary>
    public DateTimeOffset AbsoluteExpiresAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    /// <summary>
    /// Abre uma sessão nova para <paramref name="userId"/> com um token aleatório de 256 bits (devolvido em base64url,
    /// para o cookie; a sessão guarda só o hash dele). Cada entrada tem um token novo (sem fixação de sessão).
    /// </summary>
    public static (Session Session, string Token) Start(Guid userId, SignInMethod method, DateTimeOffset nowUtc)
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        var absolute = nowUtc + AbsoluteLifetime;
        var session = new Session
        {
            IdHash = HashToken(token),
            UserId = userId,
            Method = method,
            CreatedAtUtc = nowUtc,
            LastSeenAtUtc = nowUtc,
            ExpiresAtUtc = Min(nowUtc + SlidingLifetime, absolute),
            AbsoluteExpiresAtUtc = absolute,
        };
        return (session, token);
    }

    /// <summary>O hash SHA-256 (hexadecimal minúsculo) do texto do token, a chave da sessão no banco.</summary>
    public static string HashToken(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    /// <summary>A sessão vale em <paramref name="nowUtc"/>: não revogada, dentro do prazo deslizante e do teto.</summary>
    public bool IsActive(DateTimeOffset nowUtc) =>
        RevokedAtUtc is null && nowUtc < ExpiresAtUtc && nowUtc < AbsoluteExpiresAtUtc;

    /// <summary>
    /// Registra o uso: passada pelo menos <see cref="RenewalInterval"/> desde a última renovação, o prazo vai para
    /// agora + <see cref="SlidingLifetime"/>, nunca além do teto. Devolve se houve renovação (algo a gravar).
    /// </summary>
    public bool Touch(DateTimeOffset nowUtc)
    {
        if (!IsActive(nowUtc) || nowUtc - LastSeenAtUtc < RenewalInterval)
        {
            return false;
        }

        LastSeenAtUtc = nowUtc;
        ExpiresAtUtc = Min(nowUtc + SlidingLifetime, AbsoluteExpiresAtUtc);
        return true;
    }

    /// <summary>Revoga a sessão no servidor (logout); revogar de novo não muda a data.</summary>
    public void Revoke(DateTimeOffset nowUtc) => RevokedAtUtc ??= nowUtc;

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;
}
