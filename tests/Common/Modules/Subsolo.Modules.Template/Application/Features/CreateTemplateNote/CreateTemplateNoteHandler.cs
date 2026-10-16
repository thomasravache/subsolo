using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Modules.Template.Application.Features.CreateTemplateNote;

/// <summary>Dublê de teste: registra a chamada e devolve um id novo.</summary>
internal sealed class CreateTemplateNoteHandler(TemplateHandlerCalls calls) : ICommandHandler<CreateTemplateNoteCommand, Guid>
{
    public Task<Result<Guid>> Handle(CreateTemplateNoteCommand command, CancellationToken cancellationToken)
    {
        calls.Record(nameof(CreateTemplateNoteCommand));
        return Task.FromResult(Result.Success(Guid.CreateVersion7()));
    }
}
