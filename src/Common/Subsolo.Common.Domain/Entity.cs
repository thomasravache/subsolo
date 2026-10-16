namespace Subsolo.Common.Domain;

/// <summary>SPEC-0007 — base das entidades de domínio, que acumulam eventos de domínio até serem salvas.</summary>
public abstract class Entity
{
    /// <summary>Eventos levantados e ainda não gravados na outbox.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => throw new NotImplementedException();

    /// <summary>Descarta os eventos pendentes (chamado depois de gravá-los na outbox).</summary>
    public void ClearDomainEvents() => throw new NotImplementedException();

    /// <summary>Levanta um evento de domínio.</summary>
    protected void Raise(IDomainEvent domainEvent) => throw new NotImplementedException();
}
