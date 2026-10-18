using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Domain.MagicLinks;

namespace Subsolo.Modules.Users.Application.Features.CleanupMagicLinkTokens;

/// <summary>SPEC-0016 — apaga os links usados ou expirados antes de agora − <see cref="Retention"/>.</summary>
internal sealed partial class CleanupMagicLinkTokensCommandHandler(
    IMagicLinkTokenRepository tokens,
    TimeProvider clock,
    ILogger<CleanupMagicLinkTokensCommandHandler> logger)
    : ICommandHandler<CleanupMagicLinkTokensCommand>
{
    /// <summary>Por quanto tempo um link usado ou expirado fica no banco.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public async Task<Result> Handle(CleanupMagicLinkTokensCommand command, CancellationToken cancellationToken)
    {
        var deleted = await tokens.DeleteStaleAsync(clock.GetUtcNow() - Retention, cancellationToken);
        if (deleted > 0)
        {
            LogDeleted(logger, deleted);
        }

        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Limpeza dos links mágicos: {Deleted} apagados")]
    private static partial void LogDeleted(ILogger logger, int deleted);
}
