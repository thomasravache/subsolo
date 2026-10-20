namespace Subsolo.Modules.Site.Application;

/// <summary>SPEC-0028 — grava as mudanças do módulo Site numa transação.</summary>
public interface ISiteUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
