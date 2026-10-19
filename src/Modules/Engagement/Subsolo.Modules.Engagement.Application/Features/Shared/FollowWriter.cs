using Microsoft.Extensions.Logging;
using Subsolo.Modules.Engagement.Application.Diagnostics;
using Subsolo.Modules.Engagement.Domain.Follows;

namespace Subsolo.Modules.Engagement.Application.Features.Shared;

/// <summary>
/// SPEC-0027 — grava o follow de um alvo já validado, idempotente: quem já segue não ganha outro (nem nova data); a
/// corrida de dois PUT iguais termina com um follow só (a chave primária decide e o perdedor também tem sucesso).
/// </summary>
internal static partial class FollowWriter
{
    public static async Task FollowAsync(
        IFollowRepository follows,
        IEngagementUnitOfWork unitOfWork,
        Guid userId,
        FollowTarget target,
        DateTimeOffset now,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (await follows.ExistsAsync(userId, target, cancellationToken))
        {
            return;
        }

        follows.Add(Follow.Create(userId, target, now));
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (FollowAlreadyExistsException)
        {
            // Outro PUT igual gravou antes: o fã já segue o alvo (idempotente).
            return;
        }

        EngagementTelemetry.Followed(target.Kind);
        LogFollowed(logger, userId, target.Kind, target.Key);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuário {UserId} passou a seguir {TargetKind} {TargetKey}")]
    private static partial void LogFollowed(ILogger logger, Guid userId, string targetKind, string targetKey);
}
