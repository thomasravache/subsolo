using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Engagement.Application.Diagnostics;

namespace Subsolo.Modules.Engagement.Application.Features.UnattendShow;

/// <summary>
/// SPEC-0029 — apaga o "vou" do fã no show, se houver. Não valida o show: desmarcar o que não se marcou (ou um show que
/// não existe mais, cancelado ou passado) também é sucesso.
/// </summary>
internal sealed partial class UnattendShowCommandHandler(
    IAttendancesMaintenance maintenance,
    ILogger<UnattendShowCommandHandler> logger) : ICommandHandler<UnattendShowCommand>
{
    public async Task<Result> Handle(UnattendShowCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var deleted = await maintenance.DeleteAsync(command.UserId, command.ShowId, cancellationToken);
        if (deleted > 0)
        {
            EngagementTelemetry.Unattended();
            LogUnattended(logger, command.UserId, command.ShowId);
        }

        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuário {UserId} desmarcou vou no show {ShowId}")]
    private static partial void LogUnattended(ILogger logger, Guid userId, Guid showId);
}
