using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Site.Domain.Supporters;

namespace Subsolo.Modules.Site.Application.Features.RemoveSupporter;

/// <summary>SPEC-0028 — remove o apoiador; inexistente → <see cref="SiteErrors.SupporterNotFound"/>.</summary>
internal sealed partial class RemoveSupporterCommandHandler(
    ISupporterRepository supporters,
    ISiteUnitOfWork unitOfWork,
    ILogger<RemoveSupporterCommandHandler> logger) : ICommandHandler<RemoveSupporterCommand>
{
    public async Task<Result> Handle(RemoveSupporterCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await supporters.GetAsync(command.Id, cancellationToken) is not { } supporter)
        {
            return Result.Failure(SiteErrors.SupporterNotFoundError());
        }

        supporters.Remove(supporter);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogSupporterRemoved(logger, command.AdminId, supporter.Id);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Auditoria: admin {AdminId} removeu o apoiador {SupporterId}")]
    private static partial void LogSupporterRemoved(ILogger logger, Guid? adminId, Guid supporterId);
}
