using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Modules.Users.Domain.MagicLinks;

namespace Subsolo.Modules.Users.Application.Features.RequestMagicLink;

/// <summary>
/// SPEC-0016 — valida e normaliza o e-mail, aplica o limite por e-mail (3 links a cada 15 min; acima dele, sucesso
/// sem envio), grava o link (só o hash do token) e manda o e-mail <see cref="EmailTemplate.MagicLink"/> pela
/// <see cref="INotificationsApi"/> com <c>link = {App:PublicUrl}/entrar/confirmar?token=…</c> e <c>minutes = 15</c>.
/// </summary>
/// <remarks>Scaffold do Test-writer: sem lógica (a implementação é do Green).</remarks>
#pragma warning disable CS9113 // scaffold: os parâmetros são usados pela implementação do Green
internal sealed class RequestMagicLinkCommandHandler(
    IMagicLinkTokenRepository tokens,
    IUsersUnitOfWork unitOfWork,
    INotificationsApi notifications,
    IOptions<PublicUrlOptions> publicUrl,
    TimeProvider clock,
    ILogger<RequestMagicLinkCommandHandler> logger)
    : ICommandHandler<RequestMagicLinkCommand>
#pragma warning restore CS9113
{
    /// <summary>Links por e-mail dentro de <see cref="EmailWindow"/>.</summary>
    public const int EmailLimit = 3;

    /// <summary>Janela do limite por e-mail.</summary>
    public static readonly TimeSpan EmailWindow = TimeSpan.FromMinutes(15);

    public Task<Result> Handle(RequestMagicLinkCommand command, CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0016: RequestMagicLink");
}
