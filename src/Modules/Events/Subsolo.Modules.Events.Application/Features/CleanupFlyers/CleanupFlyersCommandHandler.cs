using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Media;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Domain.Flyers;

namespace Subsolo.Modules.Events.Application.Features.CleanupFlyers;

/// <summary>
/// SPEC-0022 — apaga os flyers não ligados criados antes de agora − <see cref="Retention"/>. SPEC-0023 (H22-01):
/// primeiro as linhas, num comando só com a condição no banco (um flyer ligado a um show ou reenviado no meio da limpeza
/// não casa mais e fica), depois os arquivos só das linhas apagadas (apagar de novo um que já sumiu não falha). Se a
/// limpeza parar entre os dois passos, sobram arquivos sem linha, nunca linha sem arquivo.
/// </summary>
internal sealed partial class CleanupFlyersCommandHandler(
    IFlyerRepository flyers,
    IFileStorage storage,
    IEventsUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<CleanupFlyersCommandHandler> logger)
    : ICommandHandler<CleanupFlyersCommand>
{
    /// <summary>Por quanto tempo um flyer sem show fica guardado.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public async Task<Result> Handle(CleanupFlyersCommand command, CancellationToken cancellationToken)
    {
        // A exclusão condicional grava sozinha (comando direto no banco): nada fica para a unidade de trabalho.
        _ = unitOfWork;
        var deleted = await flyers.DeleteUnattachedCreatedBeforeAsync(clock.GetUtcNow() - Retention, cancellationToken);
        if (deleted.Count == 0)
        {
            return Result.Success();
        }

        foreach (var id in deleted)
        {
            foreach (var path in Flyer.VariantPaths(id))
            {
                await storage.DeleteAsync(path, cancellationToken);
            }
        }

        LogDeleted(logger, deleted.Count);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Limpeza dos flyers sem show: {Deleted} apagados")]
    private static partial void LogDeleted(ILogger logger, int deleted);
}
