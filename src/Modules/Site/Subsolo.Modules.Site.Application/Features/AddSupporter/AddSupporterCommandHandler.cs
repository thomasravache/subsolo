using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Site.Domain.Supporters;

namespace Subsolo.Modules.Site.Application.Features.AddSupporter;

/// <summary>
/// SPEC-0028 — grava o apoiador (nome sem os espaços das pontas; link vazio vira nenhum) e devolve o id. Registra no
/// log de auditoria quem cadastrou (só ids).
/// </summary>
internal sealed partial class AddSupporterCommandHandler(
    ISupporterRepository supporters,
    ISiteUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<AddSupporterCommandHandler> logger) : ICommandHandler<AddSupporterCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AddSupporterCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var url = string.IsNullOrWhiteSpace(command.Url) ? null : command.Url.Trim();
        var supporter = Supporter.Create(command.Name.Trim(), url, clock.GetUtcNow());
        supporters.Add(supporter);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogSupporterAdded(logger, command.AdminId, supporter.Id);
        return Result.Success(supporter.Id);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Auditoria: admin {AdminId} cadastrou o apoiador {SupporterId}")]
    private static partial void LogSupporterAdded(ILogger logger, Guid? adminId, Guid supporterId);
}
