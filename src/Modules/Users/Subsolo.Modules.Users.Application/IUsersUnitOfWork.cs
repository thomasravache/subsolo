namespace Subsolo.Modules.Users.Application;

/// <summary>SPEC-0014 — grava as mudanças do módulo Users numa transação (com os eventos de domínio na outbox).</summary>
public interface IUsersUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
