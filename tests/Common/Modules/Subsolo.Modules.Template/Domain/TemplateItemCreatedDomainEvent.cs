using Subsolo.Common.Domain;

namespace Subsolo.Modules.Template.Domain;

/// <summary>Dublê de teste: evento de domínio levantado na criação de um <see cref="TemplateItem"/>.</summary>
public sealed record TemplateItemCreatedDomainEvent(Guid ItemId, string Name) : IDomainEvent;
