using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.NotifyNewShow;

/// <summary>
/// SPEC-0031 — consumidor de <see cref="ShowPublishedIntegrationEvent"/> (pela inbox, idempotente): show ainda
/// <c>scheduled</c> e futuro → registra em <c>published_shows</c> e enfileira o aviso <c>new_show</c>
/// (<see cref="NewShowNotice"/>) para os seguidores únicos da cidade e dos perfis do show.
/// </summary>
// TODO(SPEC-0031): esqueleto do G1 (Test-writer). O Implementer implementa IIntegrationEventHandler<ShowPublishedIntegrationEvent>
// e recebe as dependências no construtor (o harness dos testes unitários registra IEventsCatalog, IEngagementApi,
// IFollowsReadModel, IPublishedShows, INotificationsApi, TimeProvider e ILogger<T>).
internal sealed class NotifyNewShow
{
#pragma warning disable CA1822 // esqueleto: a implementação usa as dependências da instância
    public Task Handle(ShowPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
#pragma warning restore CA1822
}
