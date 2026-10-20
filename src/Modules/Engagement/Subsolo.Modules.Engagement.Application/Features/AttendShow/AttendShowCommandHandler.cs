using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Engagement.Application.Diagnostics;
using Subsolo.Modules.Engagement.Domain.Attendances;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.AttendShow;

/// <summary>
/// SPEC-0029 — show agendado e futuro (pelo <see cref="IEventsCatalog.GetShowStateAsync"/>) → grava o "vou" (se ainda
/// não existe; marcar de novo não duplica nem muda a data); cancelado, adiado ou já começado →
/// <see cref="EngagementErrors.ShowNotAttendable"/>; inexistente → <see cref="EngagementErrors.ShowNotFound"/>. A corrida
/// de dois PUT iguais termina com um "vou" só (a chave primária decide e o perdedor também tem sucesso).
/// </summary>
internal sealed partial class AttendShowCommandHandler(
    IEventsCatalog catalog,
    IAttendanceRepository attendances,
    IEngagementUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<AttendShowCommandHandler> logger) : ICommandHandler<AttendShowCommand>
{
    /// <summary>A única situação que aceita "vou".</summary>
    private const string Scheduled = "scheduled";

    public async Task<Result> Handle(AttendShowCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (await catalog.GetShowStateAsync(command.ShowId, cancellationToken) is not { } show)
        {
            return Result.Failure(EngagementErrors.ShowNotFoundError());
        }

        var now = clock.GetUtcNow();
        if (!string.Equals(show.Status, Scheduled, StringComparison.Ordinal) || show.StartsAtUtc <= now)
        {
            return Result.Failure(EngagementErrors.ShowNotAttendableError());
        }

        if (await attendances.ExistsAsync(command.UserId, command.ShowId, cancellationToken))
        {
            return Result.Success();
        }

        attendances.Add(Attendance.Create(command.UserId, command.ShowId, now));
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (AttendanceAlreadyExistsException)
        {
            // Outro PUT igual gravou antes: o fã já vai (idempotente).
            return Result.Success();
        }

        EngagementTelemetry.Attended();
        LogAttended(logger, command.UserId, command.ShowId);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuário {UserId} marcou vou no show {ShowId}")]
    private static partial void LogAttended(ILogger logger, Guid userId, Guid showId);
}
