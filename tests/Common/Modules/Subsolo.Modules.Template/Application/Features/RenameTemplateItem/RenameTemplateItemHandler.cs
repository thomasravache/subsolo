using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Modules.Template.Application.Features.RenameTemplateItem;

/// <summary>Dublê de teste: registra a chamada e devolve sucesso.</summary>
internal sealed class RenameTemplateItemHandler(TemplateHandlerCalls calls) : ICommandHandler<RenameTemplateItemCommand>
{
    public Task<Result> Handle(RenameTemplateItemCommand command, CancellationToken cancellationToken)
    {
        calls.Record(nameof(RenameTemplateItemCommand));
        return Task.FromResult(Result.Success());
    }
}
