namespace Subsolo.Modules.Engagement.Application;

/// <summary>SPEC-0027 — grava as mudanças do módulo Engagement numa transação.</summary>
public interface IEngagementUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
