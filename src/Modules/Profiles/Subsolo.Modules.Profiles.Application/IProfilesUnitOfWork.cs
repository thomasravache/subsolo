namespace Subsolo.Modules.Profiles.Application;

/// <summary>SPEC-0021 — grava as mudanças do módulo Profiles numa transação (com os eventos de domínio na outbox).</summary>
public interface IProfilesUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
