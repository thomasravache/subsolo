using Subsolo.Common.Application.EventBus;

namespace Subsolo.Modules.Events.IntegrationEvents;

/// <summary>
/// SPEC-0023 — show publicado na agenda (pela outbox do Events): <paramref name="ProfileIds"/> = organizador + casa (se
/// perfil) + bandas do line-up com perfil. Os avisos e lembretes consomem.
/// </summary>
public sealed record ShowPublishedIntegrationEvent(
    Guid ShowId,
    string City,
    IReadOnlyList<Guid> ProfileIds,
    DateTimeOffset StartsAtUtc) : IIntegrationEvent
{
    public Guid Id { get; init; }

    public DateTime OccurredOnUtc { get; init; }
}

/// <summary>
/// SPEC-0023 — show editado ou com a situação mudada (<paramref name="Status"/> = <c>scheduled</c> | <c>cancelled</c> |
/// <c>postponed</c>); cancelado ou adiado sai dos lembretes.
/// </summary>
public sealed record ShowChangedIntegrationEvent(Guid ShowId, string Status, DateTimeOffset StartsAtUtc) : IIntegrationEvent
{
    public Guid Id { get; init; }

    public DateTime OccurredOnUtc { get; init; }
}
