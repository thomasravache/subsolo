namespace Subsolo.Common.Domain;

/// <summary>
/// SPEC-0007 — base dos eventos de domínio: <see cref="Id"/> (Guid v7) e <see cref="OccurredOnUtc"/> preenchidos na
/// criação. Os setters <c>init</c> permitem que a desserialização da outbox restaure os valores originais, para que
/// uma nova tentativa publique o evento de integração com o mesmo Id.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
