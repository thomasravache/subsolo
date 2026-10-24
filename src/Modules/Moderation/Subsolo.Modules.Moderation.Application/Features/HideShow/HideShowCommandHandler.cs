using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Moderation.Application.Diagnostics;
using Subsolo.Modules.Moderation.Domain.Reports;

namespace Subsolo.Modules.Moderation.Application.Features.HideShow;

/// <summary>
/// SPEC-0033 — sem denúncia pendente do show → <see cref="ModerationErrors.NoPendingReports"/>; senão todas viram
/// <c>hidden</c> (com a nota interna) e o <see cref="ShowHiddenByModerationDomainEvent"/> vai para a outbox na mesma
/// transação (o Events tira o show das leituras públicas ao consumi-lo).
/// </summary>
internal sealed partial class HideShowCommandHandler(
    IReportRepository reports,
    IModerationUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<HideShowCommandHandler> logger)
    : ICommandHandler<HideShowCommand>
{
    public async Task<Result> Handle(HideShowCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var pending = await reports.ListPendingOfShowAsync(command.ShowId, cancellationToken);
        if (pending.Count == 0)
        {
            return Result.Failure(ModerationErrors.NoPendingReportsError());
        }

        Report.HideShow(pending, command.AdminId, command.Note, clock.GetUtcNow());
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ModerationConflictException exception) when (exception.Conflict == ModerationConflict.AlreadyDecided)
        {
            return Result.Failure(ModerationErrors.NoPendingReportsError());
        }

        ModerationTelemetry.Decided(ReportStatuses.Hidden);
        LogHidden(logger, command.AdminId, command.ShowId, pending.Count);
        return Result.Success();
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Auditoria: admin {AdminId} ocultou o show {ShowId} ({Count} denúncias resolvidas)")]
    private static partial void LogHidden(ILogger logger, Guid adminId, Guid showId, int count);
}
