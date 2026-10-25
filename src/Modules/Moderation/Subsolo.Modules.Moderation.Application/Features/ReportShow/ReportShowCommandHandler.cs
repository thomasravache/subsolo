using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Moderation.Application.Diagnostics;
using Subsolo.Modules.Moderation.Domain.Reports;

namespace Subsolo.Modules.Moderation.Application.Features.ReportShow;

/// <summary>
/// SPEC-0033 — show inexistente ou oculto (o <see cref="IEventsCatalog"/> não o devolve) → <c>events.show_not_found</c>;
/// SPEC-0037 (H33-05) — também o show que o próprio Moderation já decidiu ocultar (há denúncia <c>hidden</c>), antes de o
/// Events consumir a decisão;
/// o fã já tem denúncia pendente dele → <see cref="ModerationErrors.AlreadyReported"/>; senão grava a denúncia pendente.
/// SPEC-0039 (H37-02): as leituras e a gravação acontecem sob a trava do show (<see cref="IShowLocks"/>), a
/// mesma do <c>HideShow</c>: a denúncia que chega durante a decisão espera o commit dela e vê o show oculto.
/// </summary>
internal sealed partial class ReportShowCommandHandler(
    IEventsCatalog catalog,
    IReportRepository reports,
    IModerationUnitOfWork unitOfWork,
    IShowLocks locks,
    TimeProvider clock,
    ILogger<ReportShowCommandHandler> logger)
    : ICommandHandler<ReportShowCommand, ReportCreated>
{
    public async Task<Result<ReportCreated>> Handle(ReportShowCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var held = await locks.AcquireAsync(command.ShowId, cancellationToken);
        if (await catalog.GetShowSummaryAsync(command.ShowId, cancellationToken) is null
            || await reports.IsHiddenAsync(command.ShowId, cancellationToken))
        {
            return Result.Failure<ReportCreated>(ModerationErrors.ShowNotFoundError());
        }

        if (await reports.HasPendingAsync(command.ShowId, command.UserId, cancellationToken))
        {
            return Result.Failure<ReportCreated>(ModerationErrors.AlreadyReportedError());
        }

        var report = Report.Submit(command.ShowId, command.UserId, command.Reason!, command.Details, clock.GetUtcNow());
        reports.Add(report);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await held.CommitAsync(cancellationToken);
        }
        catch (ModerationConflictException exception) when (exception.Conflict == ModerationConflict.AlreadyReported)
        {
            // Outro pedido do mesmo fã chegou antes (índice único parcial).
            return Result.Failure<ReportCreated>(ModerationErrors.AlreadyReportedError());
        }

        ModerationTelemetry.Reported(report.Reason);
        LogReported(logger, report.Id, report.ShowId, report.Reason);
        return Result.Success(new ReportCreated(report.Id));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Denúncia {ReportId} do show {ShowId} ({Reason})")]
    private static partial void LogReported(ILogger logger, Guid reportId, Guid showId, string reason);
}
