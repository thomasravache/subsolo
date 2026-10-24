using Subsolo.Modules.Notifications.Application.Features.Enqueue;
using Subsolo.Modules.Notifications.Domain.Preferences;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Notifications.Application;

/// <summary>
/// SPEC-0030 — o schema <c>notifications</c> visto pelos casos de uso: inscrições de push, preferências, avisos e
/// entregas (§6). Implementado no Infrastructure com SQL em lote (o <c>EnqueueAsync</c> grava milhares de entregas de uma
/// vez, §5).
/// </summary>
internal interface INotificationsStore
{
    /// <summary>
    /// Grava (ou atualiza, pelo endpoint) a inscrição de <paramref name="userId"/>; a primeira inscrição do usuário liga o
    /// show novo por push (§2, Emenda de 2026-10-23) e grava as preferências. SPEC-0031 (H30-07): só sem a linha de
    /// preferências — com ela, as escolhas do fã não mudam.
    /// </summary>
    Task SubscribeAsync(Guid userId, string endpoint, string p256dh, string auth, DateTimeOffset now, CancellationToken ct);

    /// <summary>Remove a inscrição <paramref name="endpoint"/> de <paramref name="userId"/> (sem ela, nada muda).</summary>
    Task UnsubscribeAsync(Guid userId, string endpoint, CancellationToken ct);

    /// <summary>As preferências gravadas (ou os padrões) e quantos aparelhos estão inscritos.</summary>
    Task<(NotificationPreferences Preferences, int PushDevices)> PreferencesAsync(Guid userId, CancellationToken ct);

    /// <summary>Grava as preferências de <paramref name="userId"/>.</summary>
    Task SavePreferencesAsync(Guid userId, NotificationPreferences preferences, CancellationToken ct);

    /// <summary>Desliga o e-mail do tipo <paramref name="kind"/> (<c>digest</c> | <c>reminder</c>) de <paramref name="userId"/>.</summary>
    Task TurnOffEmailAsync(Guid userId, string kind, CancellationToken ct);

    /// <summary>Preferências (ou os padrões) e se há aparelho inscrito, de cada usuário de <paramref name="userIds"/>.</summary>
    Task<IReadOnlyList<RecipientState>> RecipientsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    /// <summary>As entregas já registradas para <paramref name="dedupeKey"/> entre <paramref name="userIds"/>.</summary>
    Task<IReadOnlySet<PlannedDelivery>> RegisteredAsync(string dedupeKey, IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    /// <summary>
    /// Grava o aviso e as entregas pendentes (em lote); uma entrega que já existe para (usuário, chave, canal) é ignorada.
    /// </summary>
    Task AddDeliveriesAsync(Notice notice, IReadOnlyList<PlannedDelivery> deliveries, DateTimeOffset now, CancellationToken ct);

    /// <summary>Apaga inscrições, preferências e entregas de <paramref name="userId"/>.</summary>
    Task ForgetAsync(Guid userId, CancellationToken ct);

    /// <summary>O que o schema guarda de <paramref name="userId"/>, sem as chaves das inscrições; nada → <see langword="null"/>.</summary>
    Task<NotificationsExport?> ExportAsync(Guid userId, CancellationToken ct);
}

/// <summary>SPEC-0030 — o que o <c>EnqueueAsync</c> lê de um destinatário no schema.</summary>
internal sealed record RecipientState(Guid UserId, NotificationPreferences Preferences, bool HasPushSubscription);

/// <summary>
/// SPEC-0030 — a seção <c>notifications</c> do export (ADR-0008): as preferências, os aparelhos inscritos (só o serviço
/// de push e as datas: o endpoint e as chaves de criptografia não saem) e as entregas.
/// </summary>
internal sealed record NotificationsExport(
    ExportedPreferences? Preferences,
    IReadOnlyList<ExportedDevice> PushDevices,
    IReadOnlyList<ExportedDelivery> Deliveries);

internal sealed record ExportedPreferences(bool NewShowPush, bool DailyDigestEmail, bool ReminderPush, bool ReminderEmail);

internal sealed record ExportedDevice(string PushService, DateTimeOffset CreatedAtUtc, DateTimeOffset? LastSuccessAtUtc);

internal sealed record ExportedDelivery(string Kind, string Channel, string Status, DateTimeOffset CreatedAtUtc);
