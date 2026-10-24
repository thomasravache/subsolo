using Microsoft.EntityFrameworkCore;
using Subsolo.Common.Infrastructure.Database;

namespace Subsolo.Modules.Notifications.Infrastructure.Persistence;

/// <summary>
/// SPEC-0030 — DbContext do módulo Notifications, no schema <see cref="SchemaName"/> (ADR-0002): inscrições de push,
/// preferências, avisos e entregas (§6), além da outbox/inbox comuns. As gravações em lote do
/// <see cref="NotificationsStore"/> usam SQL direto na conexão dele.
/// </summary>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
    : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "notifications";

    /// <summary>Índice único do endpoint da inscrição.</summary>
    public const string SubscriptionEndpointIndex = "IX_push_subscriptions_endpoint";

    /// <summary>Índice único (usuário, chave, canal) das entregas: a idempotência do <c>EnqueueAsync</c> (ADR-0014).</summary>
    public const string DeliveriesUniqueIndex = "IX_deliveries_user_id_dedupe_key_channel";

    internal DbSet<PushSubscriptionRow> PushSubscriptions => Set<PushSubscriptionRow>();

    internal DbSet<PreferencesRow> Preferences => Set<PreferencesRow>();

    internal DbSet<NoticeRow> Notices => Set<NoticeRow>();

    internal DbSet<DeliveryRow> Deliveries => Set<DeliveryRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
    }
}
