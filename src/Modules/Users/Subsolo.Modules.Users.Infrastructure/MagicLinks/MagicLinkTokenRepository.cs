using Subsolo.Modules.Users.Domain.MagicLinks;

namespace Subsolo.Modules.Users.Infrastructure.MagicLinks;

/// <summary>SPEC-0016 — links mágicos na tabela <c>users.magic_link_tokens</c>.</summary>
/// <remarks>
/// Scaffold do Test-writer: registrado só para o host montar o contêiner com os handlers novos; sem lógica (a tabela,
/// a migração e as consultas são do Green).
/// </remarks>
#pragma warning disable CS9113 // scaffold: o DbContext é usado pela implementação do Green
internal sealed class MagicLinkTokenRepository(UsersDbContext db) : IMagicLinkTokenRepository
#pragma warning restore CS9113
{
    public void Add(MagicLinkToken token) =>
        throw new NotImplementedException("SPEC-0016: MagicLinkTokenRepository.Add");

    public Task<MagicLinkToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0016: MagicLinkTokenRepository.GetByHashAsync");

    public Task<int> CountIssuedSinceAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0016: MagicLinkTokenRepository.CountIssuedSinceAsync");

    public Task<bool> TryMarkUsedAsync(string tokenHash, DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0016: MagicLinkTokenRepository.TryMarkUsedAsync");

    public Task<int> DeleteStaleAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0016: MagicLinkTokenRepository.DeleteStaleAsync");
}
