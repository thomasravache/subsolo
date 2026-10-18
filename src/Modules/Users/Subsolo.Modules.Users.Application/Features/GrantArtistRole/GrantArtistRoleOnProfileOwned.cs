using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.EventBus;
using Subsolo.Modules.Users.Domain.Users;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Users.Application.Features.GrantArtistRole;

/// <summary>
/// SPEC-0021 (Emenda v2, ADR-0003) — consome <see cref="ArtistRoleGrantedIntegrationEvent"/> (publicado pelo Profiles
/// quando um perfil ganha dono) pela inbox do Users e dá o papel <see cref="UserRoles.Artist"/> ao usuário, uma vez só
/// (idempotente: a inbox ignora o evento repetido e o papel não se duplica). Usuário já excluído → nada a fazer. O log
/// leva só os ids.
/// </summary>
internal sealed partial class GrantArtistRoleOnProfileOwned(
    IUserRepository users,
    IUsersUnitOfWork unitOfWork,
    ILogger<GrantArtistRoleOnProfileOwned> logger)
    : IIntegrationEventHandler<ArtistRoleGrantedIntegrationEvent>
{
    public async Task Handle(ArtistRoleGrantedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var user = await users.GetByIdAsync(integrationEvent.UserId, cancellationToken);
        if (user is null || !user.GrantArtistRole())
        {
            return;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogGranted(logger, user.Id, integrationEvent.ProfileId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuário {UserId} ganhou o papel artist (perfil {ProfileId})")]
    private static partial void LogGranted(ILogger logger, Guid userId, Guid profileId);
}
