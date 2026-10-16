namespace Subsolo.Common.Domain;

/// <summary>
/// SPEC-0007 — evento de domínio levantado por uma <see cref="Entity"/>. Ao salvar o <c>ModuleDbContext</c>, os
/// eventos pendentes são gravados na outbox do módulo na mesma transação.
/// </summary>
public interface IDomainEvent
{
    /// <summary>Identidade estável do evento (Guid v7); o evento de integração derivado reaproveita este valor.</summary>
    Guid Id { get; }

    DateTime OccurredOnUtc { get; }
}
