using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Application.Diagnostics;
using Subsolo.Modules.Users.Application.Features.SignIn;
using Subsolo.Modules.Users.Domain.MagicLinks;
using Subsolo.Modules.Users.Domain.Sessions;

namespace Subsolo.Modules.Users.Application.Features.ConsumeMagicLink;

/// <summary>
/// SPEC-0016 — consome o link uma única vez (marcado como usado de forma atômica) e entra pelo
/// <see cref="ISignInService"/> (método <c>magic_link</c>, sessão nova e cookie); inexistente, expirado ou já usado →
/// <see cref="UsersErrors.MagicLinkInvalid"/> (o mesmo código para os três).
/// </summary>
/// <remarks>Os logs não levam o e-mail nem o token (ADR-0008).</remarks>
internal sealed partial class ConsumeMagicLinkCommandHandler(
    IMagicLinkTokenRepository tokens,
    ISignInService signIn,
    TimeProvider clock,
    ILogger<ConsumeMagicLinkCommandHandler> logger)
    : ICommandHandler<ConsumeMagicLinkCommand, ConsumeMagicLinkResponse>
{
    public async Task<Result<ConsumeMagicLinkResponse>> Handle(ConsumeMagicLinkCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = clock.GetUtcNow();
        var hash = MagicLinkToken.HashToken(command.Token ?? string.Empty);
        var token = await tokens.GetByHashAsync(hash, cancellationToken);

        // Marcar como usado é atômico no banco: de dois consumos simultâneos, só um passa daqui.
        if (token is null || !token.IsUsable(now) || !await tokens.TryMarkUsedAsync(hash, now, cancellationToken))
        {
            UsersTelemetry.MagicLinkConsumed(UsersTelemetry.ConsumeInvalid);
            LogInvalid(logger);
            return Result.Failure<ConsumeMagicLinkResponse>(UsersErrors.MagicLinkInvalidError());
        }

        var signedIn = await signIn.SignInAsync(token.Email, SignInMethod.MagicLink, cancellationToken);
        if (signedIn.IsFailure)
        {
            return Result.Failure<ConsumeMagicLinkResponse>(signedIn.Error);
        }

        UsersTelemetry.MagicLinkConsumed(UsersTelemetry.ConsumeOk);
        return Result.Success(new ConsumeMagicLinkResponse(token.ReturnTo));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Link mágico recusado: inexistente, expirado ou já usado")]
    private static partial void LogInvalid(ILogger logger);
}
