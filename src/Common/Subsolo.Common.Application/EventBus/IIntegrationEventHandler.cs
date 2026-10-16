using System.Diagnostics.CodeAnalysis;
namespace Subsolo.Common.Application.EventBus;

/// <summary>SPEC-0007 — handler de um evento de integração de outro módulo, executado pelo job <c>ProcessInbox</c>.</summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Nome fixado pelo contrato da SPEC-0007 (handler de evento de integração, não um delegate EventHandler).")]
public interface IIntegrationEventHandler<in T>
    where T : IIntegrationEvent
{
    Task Handle(T integrationEvent, CancellationToken cancellationToken);
}
