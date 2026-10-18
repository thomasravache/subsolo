using Subsolo.Common.Application.EventBus;

namespace Subsolo.Modules.Users.IntegrationEvents;

/// <summary>
/// SPEC-0019 — o titular excluiu a conta, publicada pela outbox para os módulos que guardam dados do usuário apagarem o
/// que é dele. Leva só o id e a data: o e-mail não sai do módulo Users.
/// </summary>
public sealed record UserDeletedIntegrationEvent(Guid UserId, DateTimeOffset DeletedAtUtc) : IIntegrationEvent
{
    public Guid Id { get; init; }

    public DateTime OccurredOnUtc { get; init; }
}
