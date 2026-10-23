using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Users.Infrastructure;

/// <summary>
/// SPEC-0030 — seção <see cref="SectionName"/> do export de dados pessoais (SPEC-0019, ADR-0008): o que o Notifications
/// guarda do titular (<see cref="INotificationsApi.ExportAsync"/>: preferências, aparelhos sem as chaves de criptografia e
/// entregas); sem nada, a seção é omitida. Mora no Users porque o Notifications não depende de módulo de domínio (ADR-0001).
/// </summary>
internal sealed class NotificationsPersonalDataSection(INotificationsApi notifications) : IPersonalDataSection
{
    public const string SectionName = "notifications";

    public string Name => SectionName;

    public Task<object?> ExportAsync(Guid userId, CancellationToken ct) => notifications.ExportAsync(userId, ct);
}
