using Subsolo.Common.Application.EventBus;

namespace Subsolo.Modules.Profiles.IntegrationEvents;

/// <summary>
/// SPEC-0021 — o perfil <paramref name="ProfileId"/> ganhou o dono <paramref name="UserId"/> (pedido novo ou
/// reivindicação aprovados pelo admin), publicado pela outbox; o módulo Users consome e dá o papel <c>artist</c>. Leva
/// só ids.
/// </summary>
public sealed record ProfileOwnerAssignedIntegrationEvent(Guid ProfileId, Guid UserId) : IIntegrationEvent
{
    public Guid Id { get; init; }

    public DateTime OccurredOnUtc { get; init; }
}
