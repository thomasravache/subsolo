using Subsolo.Common.Application.EventBus;
using Subsolo.Common.Domain;

namespace Subsolo.Modules.EntitySample.IntegrationEvents;

/// <summary>
/// SPEC-0018 IT-05 (Emenda 1, ADR-0001 esclarecido) — dublê: evento de integração que expõe uma entidade de
/// Common.Domain; continua proibido (só os tipos de resultado são liberados).
/// </summary>
public sealed record EntityExposed(Guid Id, DateTime OccurredOnUtc, Entity Source) : IIntegrationEvent;

/// <summary>Dublê: contrato público que expõe um evento de domínio; continua proibido.</summary>
public interface IDomainEventSampleApi
{
    DomainEvent? LastEvent { get; }
}
