using System.Diagnostics.CodeAnalysis;
namespace Subsolo.Common.Application.EventBus;

/// <summary>
/// SPEC-0007 — handler de um evento de integração de outro módulo, executado pelo job <c>ProcessInbox</c>. Uma
/// exceção agenda nova tentativa; a mensagem dela vai para a coluna de erro e para o log, então não deve conter
/// dados pessoais (ADR-0008).
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Nome fixado pelo contrato da SPEC-0007 (handler de evento de integração, não um delegate EventHandler).")]
public interface IIntegrationEventHandler<in T>
    where T : IIntegrationEvent
{
    Task Handle(T integrationEvent, CancellationToken cancellationToken);
}
