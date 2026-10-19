using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Subsolo.Modules.Events.Application;

namespace Subsolo.Modules.Events.Infrastructure.Flyers;

/// <summary>
/// SPEC-0026 (H24-02) — <see cref="IFlyerLocks"/> com <c>pg_advisory_xact_lock</c> numa transação do
/// <see cref="EventsDbContext"/> (a chave é o hash de <c>events.flyer:{id}</c>): a trava some com o fim da transação, mesmo
/// se o processo cair no meio.
/// </summary>
internal sealed class FlyerLocks(EventsDbContext db) : IFlyerLocks
{
    public async Task<IFlyerLock> AcquireAsync(string flyerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flyerId);
        var key = $"events.flyer:{flyerId}";
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
            return new Held(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class Held(IDbContextTransaction transaction) : IFlyerLock
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        // Sem commit, o descarte da transação desfaz o que foi gravado e solta a trava.
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
