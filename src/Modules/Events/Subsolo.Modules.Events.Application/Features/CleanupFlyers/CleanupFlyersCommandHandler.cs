using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Media;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Domain.Flyers;

namespace Subsolo.Modules.Events.Application.Features.CleanupFlyers;

/// <summary>
/// SPEC-0022 — apaga os flyers não ligados criados antes de agora − <see cref="Retention"/>: primeiro os arquivos
/// (apagar de novo um que já sumiu não falha), depois as linhas — se a gravação falhar, a próxima limpeza refaz.
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
        var stale = await flyers.ListUnattachedCreatedBeforeAsync(clock.GetUtcNow() - Retention, cancellationToken);
        if (stale.Count == 0)
        {
            return Result.Success();
        }

        foreach (var flyer in stale)
        {
            foreach (var path in Flyer.VariantPaths(flyer.Id))
            {
                await storage.DeleteAsync(path, cancellationToken);
            }

            flyers.Remove(flyer);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogDeleted(logger, stale.Count);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Limpeza dos flyers sem show: {Deleted} apagados")]
    private static partial void LogDeleted(ILogger logger, int deleted);
}
