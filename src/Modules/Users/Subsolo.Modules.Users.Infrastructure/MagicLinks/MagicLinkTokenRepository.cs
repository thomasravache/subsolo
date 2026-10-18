using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Users.Domain.MagicLinks;

namespace Subsolo.Modules.Users.Infrastructure.MagicLinks;

/// <summary>
/// SPEC-0016 — links mágicos na tabela <c>users.magic_link_tokens</c>. O uso é marcado com um <c>UPDATE</c> condicional
/// (não usado e dentro da validade), atômico no banco: de dois consumos simultâneos do mesmo token, só um altera a linha.
/// </summary>
internal sealed class MagicLinkTokenRepository(UsersDbContext db) : IMagicLinkTokenRepository
{
    public void Add(MagicLinkToken token) => db.MagicLinkTokens.Add(token);

    public Task<MagicLinkToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.MagicLinkTokens.AsNoTracking().SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public Task<int> CountIssuedSinceAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
        db.MagicLinkTokens.CountAsync(
            token => token.Email == normalizedEmail && token.CreatedAtUtc > sinceUtc, cancellationToken);

    public async Task<bool> TryMarkUsedAsync(string tokenHash, DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
        await db.MagicLinkTokens
            .Where(token => token.TokenHash == tokenHash && token.UsedAtUtc == null && token.ExpiresAtUtc > nowUtc)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.UsedAtUtc, nowUtc), cancellationToken) == 1;

    public Task<int> DeleteStaleAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken) =>
        db.MagicLinkTokens
            .Where(token => token.UsedAtUtc < cutoffUtc || token.ExpiresAtUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task<IReadOnlyList<MagicLinkToken>> ListByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        await db.MagicLinkTokens.Where(token => token.Email == normalizedEmail).ToListAsync(cancellationToken);

    public void Remove(MagicLinkToken token) => db.MagicLinkTokens.Remove(token);
}
