namespace Subsolo.Modules.Events.Application;

/// <summary>SPEC-0017 — grava as mudanças do módulo Events numa transação (com os eventos de domínio na outbox).</summary>
public interface IEventsUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
