using System.Diagnostics.CodeAnalysis;
using Subsolo.Common.Domain;

namespace Subsolo.Common.Application.Messaging;

/// <summary>
/// SPEC-0007 — handler de um evento de domínio do próprio módulo, executado pelo job <c>ProcessOutbox</c> do
/// módulo a partir da outbox (é onde o módulo publica seus eventos de integração pelo <c>IEventBus</c>).
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Handler de evento de domínio (padrão do ADR-0001), não um delegate EventHandler.")]
public interface IDomainEventHandler<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    Task Handle(TDomainEvent domainEvent, CancellationToken cancellationToken);
}
