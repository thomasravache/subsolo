using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Moderation.Application.Diagnostics;
using Subsolo.Modules.Moderation.Domain.Reports;

namespace Subsolo.Modules.Moderation.Application.Features.KeepShow;

/// <summary>
/// SPEC-0033 — sem denúncia pendente do show (já decidido ou nunca denunciado) → <see cref="ModerationErrors.NoPendingReports"/>;
/// senão todas viram <c>kept</c>, com quando e quem decidiu. Decisão concorrente que chegou antes → o mesmo 404.
/// </summary>
internal sealed partial class KeepShowCommandHandler(
    IReportRepository reports,
    IModerationUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<KeepShowCommandHandler> logger)
    : ICommandHandler<KeepShowCommand>
{
    public async Task<Result> Handle(KeepShowCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var pending = await reports.ListPendingOfShowAsync(command.ShowId, cancellationToken);
        if (pending.Count == 0)
        {
            return Result.Failure(ModerationErrors.NoPendingReportsError());
        }

        Report.KeepShow(pending, command.AdminId, clock.GetUtcNow());
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ModerationConflictException exception) when (exception.Conflict == ModerationConflict.AlreadyDecided)
        {
            return Result.Failure(ModerationErrors.NoPendingReportsError());
        }

        ModerationTelemetry.Decided(ReportStatuses.Kept);
        LogKept(logger, command.AdminId, command.ShowId, pending.Count);
        return Result.Success();
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Auditoria: admin {AdminId} manteve o show {ShowId} ({Count} denúncias arquivadas)")]
    private static partial void LogKept(ILogger logger, Guid adminId, Guid showId, int count);
}
