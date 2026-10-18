namespace Subsolo.Modules.Users.Domain.MagicLinks;

/// <summary>SPEC-0016 — links mágicos no schema <c>users</c>, buscados pelo hash do token.</summary>
public interface IMagicLinkTokenRepository
{
    /// <summary>Inclui um link novo (gravado no próximo <c>SaveChangesAsync</c>).</summary>
    void Add(MagicLinkToken token);

    /// <summary>O link cujo token tem o hash <paramref name="tokenHash"/>, ou <see langword="null"/>.</summary>
    Task<MagicLinkToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Quantos links foram emitidos para <paramref name="normalizedEmail"/> desde <paramref name="sinceUtc"/> (limite por e-mail).</summary>
    Task<int> CountIssuedSinceAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Marca o link como usado de forma atômica no banco, só se ainda estiver usável em <paramref name="nowUtc"/>;
    /// <see langword="false"/> = inexistente, expirado, já usado ou outro consumo simultâneo chegou antes.
    /// </summary>
    Task<bool> TryMarkUsedAsync(string tokenHash, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    /// <summary>Apaga os links usados ou expirados antes de <paramref name="cutoffUtc"/>; devolve quantos.</summary>
    Task<int> DeleteStaleAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken);

    /// <summary>SPEC-0019 — os links (usados ou não) emitidos para <paramref name="normalizedEmail"/>.</summary>
    Task<IReadOnlyList<MagicLinkToken>> ListByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>SPEC-0019 — apaga o link (no próximo <c>SaveChangesAsync</c>).</summary>
    void Remove(MagicLinkToken token);
}
