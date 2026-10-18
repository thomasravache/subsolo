using Subsolo.Common.Application.EventBus;
using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Profiles.Domain.Profiles;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Profiles.Application.Features.GrantArtistRole;

/// <summary>
/// SPEC-0021 (Emenda v2) — ao processar a outbox, publica <see cref="ArtistRoleGrantedIntegrationEvent"/> (contrato do
/// Users, só ids) com o <c>Id</c> do evento de domínio (uma nova tentativa publica o mesmo evento): o Users dá o papel
/// <c>artist</c> ao novo dono.
/// </summary>
internal sealed class PublishArtistRoleGranted(IEventBus eventBus) : IDomainEventHandler<ProfileOwnerAssignedDomainEvent>
{
    public Task Handle(ProfileOwnerAssignedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return eventBus.PublishAsync(
            new ArtistRoleGrantedIntegrationEvent(domainEvent.UserId, domainEvent.ProfileId)
            {
                Id = domainEvent.Id,
                OccurredOnUtc = DateTime.SpecifyKind(domainEvent.OccurredOnUtc, DateTimeKind.Utc),
            },
            cancellationToken);
    }
}
