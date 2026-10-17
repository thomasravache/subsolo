using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Domain.MagicLinks;

namespace Subsolo.Modules.Users.Application.Features.CleanupMagicLinkTokens;

/// <summary>SPEC-0016 — apaga os links usados ou expirados antes de agora − <see cref="Retention"/>.</summary>
/// <remarks>Scaffold do Test-writer: sem lógica (a implementação é do Green).</remarks>
#pragma warning disable CS9113 // scaffold: os parâmetros são usados pela implementação do Green
internal sealed class CleanupMagicLinkTokensCommandHandler(IMagicLinkTokenRepository tokens, TimeProvider clock)
    : ICommandHandler<CleanupMagicLinkTokensCommand>
#pragma warning restore CS9113
{
    /// <summary>Por quanto tempo um link usado ou expirado fica no banco.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public Task<Result> Handle(CleanupMagicLinkTokensCommand command, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0016: CleanupMagicLinkTokens");
}
