namespace Subsolo.Common.Domain;

/// <summary>SPEC-0007 — base das entidades de domínio, que acumulam eventos de domínio até serem salvas.</summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Eventos levantados e ainda não gravados na outbox.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => [.. _domainEvents];

    /// <summary>Descarta os eventos pendentes (chamado depois de gravá-los na outbox).</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>Levanta um evento de domínio.</summary>
    protected void Raise(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }
}
