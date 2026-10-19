using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Engagement.Application.Diagnostics;

namespace Subsolo.Modules.Engagement.Application.Features.Unfollow;

/// <summary>
/// SPEC-0027 — apaga o follow do fã para o alvo, se houver. Não valida o alvo: deixar de seguir o que não se segue (ou
/// o que não existe mais) também é sucesso.
/// </summary>
internal sealed partial class UnfollowCommandHandler(
    IFollowsMaintenance maintenance,
    ILogger<UnfollowCommandHandler> logger) : ICommandHandler<UnfollowCommand>
{
    public async Task<Result> Handle(UnfollowCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var deleted = await maintenance.DeleteAsync(command.UserId, command.Target, cancellationToken);
        if (deleted > 0)
        {
            EngagementTelemetry.Unfollowed(command.Target.Kind);
            LogUnfollowed(logger, command.UserId, command.Target.Kind, command.Target.Key);
        }

        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuário {UserId} deixou de seguir {TargetKind} {TargetKey}")]
    private static partial void LogUnfollowed(ILogger logger, Guid userId, string targetKind, string targetKey);
}
