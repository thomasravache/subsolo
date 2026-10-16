using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Template.Application.Features.GetTemplateItemName;

/// <summary>Dublê de teste: consulta que devolve o nome de um item.</summary>
public sealed record GetTemplateItemNameQuery(Guid ItemId) : IQuery<string>;
