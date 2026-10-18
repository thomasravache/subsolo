using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Modules.Profiles.Application.Features.CleanupProfileRequests;

/// <summary>SPEC-0021 — apaga os pedidos decididos antes de agora − <see cref="Retention"/> (os pendentes ficam).</summary>
internal sealed partial class CleanupProfileRequestsCommandHandler(
    IProfilesMaintenance maintenance,
    TimeProvider clock,
    ILogger<CleanupProfileRequestsCommandHandler> logger)
    : ICommandHandler<CleanupProfileRequestsCommand>
{
    /// <summary>Por quanto tempo um pedido decidido fica no banco.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    public async Task<Result> Handle(CleanupProfileRequestsCommand command, CancellationToken cancellationToken)
    {
        var deleted = await maintenance.DeleteDecidedBeforeAsync(clock.GetUtcNow() - Retention, cancellationToken);
        if (deleted > 0)
        {
            LogDeleted(logger, deleted);
        }

        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Limpeza dos pedidos de perfil: {Deleted} apagados")]
    private static partial void LogDeleted(ILogger logger, int deleted);
}
