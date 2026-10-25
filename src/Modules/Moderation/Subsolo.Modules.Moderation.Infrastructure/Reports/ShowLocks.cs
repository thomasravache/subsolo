using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Subsolo.Modules.Moderation.Application;

namespace Subsolo.Modules.Moderation.Infrastructure.Reports;

/// <summary>
/// SPEC-0039 (H37-02) — <see cref="IShowLocks"/> com <c>pg_advisory_xact_lock</c> numa transação do
/// <see cref="ModerationDbContext"/> (a chave é <c>hashtextextended('moderation.show:' || show_id::text, 0)</c>): a trava
/// some com o fim da transação, mesmo se o processo cair no meio.
/// </summary>
internal sealed class ShowLocks(ModerationDbContext db) : IShowLocks
{
    public async Task<IShowLock> AcquireAsync(Guid showId, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended('moderation.show:' || {showId}::text, 0))",
                cancellationToken);
            return new Held(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class Held(IDbContextTransaction transaction) : IShowLock
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        // Sem commit, o descarte da transação desfaz o que foi gravado e solta a trava.
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
