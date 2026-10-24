using Subsolo.Modules.Events.Application;

namespace Subsolo.Events.UnitTests.Infrastructure;

/// <summary>
/// SPEC-0031 (H26-02) — trava de flyer que não trava nada: o <see cref="IFlyerLocks"/> passa a ser obrigatório nos
/// handlers de envio e limpeza, e os testes unitários (sem PostgreSQL) usam este dublê; a trava de verdade é coberta
/// pelos testes de integração da SPEC-0026.
/// </summary>
internal sealed class NoOpFlyerLocks : IFlyerLocks
{
    public Task<IFlyerLock> AcquireAsync(string flyerId, CancellationToken cancellationToken) =>
        Task.FromResult<IFlyerLock>(new Held());

    private sealed class Held : IFlyerLock
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
