using Subsolo.Common.Application.EventBus;

namespace Subsolo.Modules.Users.IntegrationEvents;

/// <summary>
/// SPEC-0014 — primeira entrada de um usuário, publicada pela outbox para o módulo de avisos. Leva só o id: o e-mail
/// não sai do módulo Users.
/// </summary>
public sealed record UserRegisteredIntegrationEvent(Guid UserId, DateTimeOffset RegisteredAtUtc) : IIntegrationEvent
{
    public Guid Id { get; init; }

    public DateTime OccurredOnUtc { get; init; }
}
