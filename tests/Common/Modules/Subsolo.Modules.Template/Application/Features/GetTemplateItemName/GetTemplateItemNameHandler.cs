using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Modules.Template.Application.Features.GetTemplateItemName;

/// <summary>Dublê de teste: registra a chamada; <see cref="Guid.Empty"/> não existe (NotFound), qualquer outro id existe.</summary>
internal sealed class GetTemplateItemNameHandler(TemplateHandlerCalls calls) : IQueryHandler<GetTemplateItemNameQuery, string>
{
    /// <summary>Código do erro devolvido para o item inexistente.</summary>
    public const string NotFoundCode = "Template.ItemNotFound";

    public Task<Result<string>> Handle(GetTemplateItemNameQuery query, CancellationToken cancellationToken)
    {
        calls.Record(nameof(GetTemplateItemNameQuery));
        return Task.FromResult(query.ItemId == Guid.Empty
            ? Result.Failure<string>(Error.NotFound(NotFoundCode, "Item não encontrado."))
            : Result.Success($"item-{query.ItemId:N}"));
    }
}
