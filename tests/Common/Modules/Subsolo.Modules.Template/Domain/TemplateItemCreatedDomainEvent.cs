using Subsolo.Common.Domain;

namespace Subsolo.Modules.Template.Domain;

/// <summary>
/// Dublê de teste: evento de domínio levantado na criação de um <see cref="TemplateItem"/>. Herda a identidade
/// estável (<see cref="DomainEvent.Id"/>) da base do contrato.
/// </summary>
public sealed record TemplateItemCreatedDomainEvent(Guid ItemId, string Name) : DomainEvent;
