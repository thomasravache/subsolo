using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Application.Features.SignIn;
using Subsolo.Modules.Users.Domain.MagicLinks;

namespace Subsolo.Modules.Users.Application.Features.ConsumeMagicLink;

/// <summary>
/// SPEC-0016 — consome o link uma única vez (marcado como usado de forma atômica) e entra pelo
/// <see cref="ISignInService"/> (método <c>magic_link</c>, sessão nova e cookie); inexistente, expirado ou já usado →
/// <see cref="UsersErrors.MagicLinkInvalid"/> (o mesmo código para os três).
/// </summary>
/// <remarks>Scaffold do Test-writer: sem lógica (a implementação é do Green).</remarks>
#pragma warning disable CS9113 // scaffold: os parâmetros são usados pela implementação do Green
internal sealed class ConsumeMagicLinkCommandHandler(
    IMagicLinkTokenRepository tokens,
    ISignInService signIn,
    TimeProvider clock,
    ILogger<ConsumeMagicLinkCommandHandler> logger)
    : ICommandHandler<ConsumeMagicLinkCommand, ConsumeMagicLinkResponse>
#pragma warning restore CS9113
{
    public Task<Result<ConsumeMagicLinkResponse>> Handle(ConsumeMagicLinkCommand command, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0016: ConsumeMagicLink");
}
