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

    /// <summary>Revoga a sessão no servidor (logout).</summary>
    public void Revoke(DateTimeOffset nowUtc) =>
        throw new NotImplementedException("SPEC-0014: Session.Revoke ainda não implementado.");
}
