using Subsolo.Common.Application.EventBus;

namespace Subsolo.Modules.Users.IntegrationEvents;

/// <summary>
/// SPEC-0021 (Emenda v2) — contrato do consumidor (ADR-0001): o usuário <paramref name="UserId"/> passou a ser dono do
/// perfil <paramref name="ProfileId"/> (pedido novo ou reivindicação aprovados pelo admin). Publicado pelo módulo
/// Profiles pela outbox; o Users consome (inbox, idempotente) e dá o papel <c>artist</c>. Leva só ids. Definido aqui, e
/// não no Profiles, para a dependência entre os módulos ser só <c>Profiles → Users</c>.
/// </summary>
public sealed record ArtistRoleGrantedIntegrationEvent(Guid UserId, Guid ProfileId) : IIntegrationEvent
{
    public Guid Id { get; init; }

    public DateTime OccurredOnUtc { get; init; }
}
