namespace Subsolo.Common.Domain;

/// <summary>SPEC-0007 — base dos eventos de domínio: <see cref="Id"/> e <see cref="OccurredOnUtc"/> preenchidos na criação.</summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid Id => throw new NotImplementedException();

    public DateTime OccurredOnUtc => throw new NotImplementedException();
}
