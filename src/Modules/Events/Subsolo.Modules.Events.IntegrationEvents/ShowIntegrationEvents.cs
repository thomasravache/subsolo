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

/// <summary>
/// SPEC-0033 — a moderação ocultou o show <paramref name="ShowId"/> em <paramref name="HiddenAtUtc"/>. Publicado pelo
/// Moderation (pela outbox dele) e consumido pelo Events, que o tira de toda leitura pública: o evento mora no contrato do
/// consumidor, para o Events não depender do Moderation (ADR-0001).
/// </summary>
public sealed record ShowHiddenByModerationIntegrationEvent(Guid ShowId, DateTimeOffset HiddenAtUtc) : IIntegrationEvent
{
    public Guid Id { get; init; }

    public DateTime OccurredOnUtc { get; init; }
}
