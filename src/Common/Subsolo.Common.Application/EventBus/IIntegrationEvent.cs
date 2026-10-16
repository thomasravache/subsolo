namespace Subsolo.Common.Application.EventBus;

/// <summary>SPEC-0007 — evento de integração: o contrato público entre módulos (<c>&lt;M&gt;.IntegrationEvents</c>).</summary>
public interface IIntegrationEvent
{
    /// <summary>Identidade do evento; a inbox usa este valor para processar cada evento uma única vez.</summary>
    Guid Id { get; }

    DateTime OccurredOnUtc { get; }
}
