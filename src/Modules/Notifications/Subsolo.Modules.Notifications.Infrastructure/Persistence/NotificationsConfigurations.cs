using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Subsolo.Modules.Notifications.Domain.Notices;

namespace Subsolo.Modules.Notifications.Infrastructure.Persistence;

/// <summary>SPEC-0030 — <c>push_subscriptions</c>: endpoint único, índice por usuário (aparelhos de cada um).</summary>
internal sealed class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscriptionRow>
{
    public void Configure(EntityTypeBuilder<PushSubscriptionRow> builder)
    {
        builder.ToTable("push_subscriptions");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(row => row.UserId).HasColumnName("user_id");
        builder.Property(row => row.Endpoint).HasColumnName("endpoint").HasColumnType("text");
        builder.Property(row => row.P256dh).HasColumnName("p256dh").HasColumnType("text");
        builder.Property(row => row.Auth).HasColumnName("auth").HasColumnType("text");
        builder.Property(row => row.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(row => row.LastSuccessAtUtc).HasColumnName("last_success_at_utc");
        builder.HasIndex(row => row.Endpoint).IsUnique().HasDatabaseName(NotificationsDbContext.SubscriptionEndpointIndex);
        builder.HasIndex(row => row.UserId);
    }
}

/// <summary>SPEC-0030 — <c>preferences</c>: uma linha por usuário.</summary>
internal sealed class PreferencesConfiguration : IEntityTypeConfiguration<PreferencesRow>
{
    public void Configure(EntityTypeBuilder<PreferencesRow> builder)
    {
        builder.ToTable("preferences");
        builder.HasKey(row => row.UserId);
        builder.Property(row => row.UserId).HasColumnName("user_id").ValueGeneratedNever();
        builder.Property(row => row.NewShowPush).HasColumnName("new_show_push");
        builder.Property(row => row.DailyDigestEmail).HasColumnName("daily_digest_email");
        builder.Property(row => row.ReminderPush).HasColumnName("reminder_push");
        builder.Property(row => row.ReminderEmail).HasColumnName("reminder_email");
    }
}

/// <summary>SPEC-0030 — <c>notices</c>: o conteúdo de cada aviso enfileirado.</summary>
internal sealed class NoticeConfiguration : IEntityTypeConfiguration<NoticeRow>
{
    public void Configure(EntityTypeBuilder<NoticeRow> builder)
    {
        builder.ToTable("notices");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(row => row.DedupeKey).HasColumnName("dedupe_key").HasColumnType("text");
        builder.Property(row => row.Kind).HasColumnName("kind").HasColumnType("text");
        builder.Property(row => row.Title).HasColumnName("title").HasColumnType("text");
        builder.Property(row => row.Body).HasColumnName("body").HasColumnType("text");
        builder.Property(row => row.Url).HasColumnName("url").HasColumnType("text");
        builder.Property(row => row.CreatedAtUtc).HasColumnName("created_at_utc");
    }
}

/// <summary>
/// SPEC-0030 — <c>deliveries</c>: única por (usuário, chave, canal) — a idempotência do <c>EnqueueAsync</c> — com o
/// índice parcial das pendentes (o job de entregas lê por <c>next_attempt_at_utc</c>).
/// </summary>
internal sealed class DeliveryConfiguration : IEntityTypeConfiguration<DeliveryRow>
{
    public void Configure(EntityTypeBuilder<DeliveryRow> builder)
    {
        builder.ToTable("deliveries");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(row => row.NoticeId).HasColumnName("notice_id");
        builder.Property(row => row.UserId).HasColumnName("user_id");
        builder.Property(row => row.DedupeKey).HasColumnName("dedupe_key").HasColumnType("text");
        builder.Property(row => row.Kind).HasColumnName("kind").HasColumnType("text");
        builder.Property(row => row.Channel).HasColumnName("channel").HasColumnType("text");
        builder.Property(row => row.Status).HasColumnName("status").HasColumnType("text");
        builder.Property(row => row.Attempts).HasColumnName("attempts");
        builder.Property(row => row.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(row => row.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");
        builder.Property(row => row.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.Property(row => row.PendingEndpoints).HasColumnName("pending_endpoints").HasColumnType("text[]");
        builder.HasIndex(row => new { row.UserId, row.DedupeKey, row.Channel })
            .IsUnique()
            .HasDatabaseName(NotificationsDbContext.DeliveriesUniqueIndex);
        builder.HasIndex(row => row.NextAttemptAtUtc)
            .HasDatabaseName("IX_deliveries_pending")
            .HasFilter($"status = '{DeliveryStatuses.Pending}'");
        builder.HasOne<NoticeRow>().WithMany().HasForeignKey(row => row.NoticeId).OnDelete(DeleteBehavior.Cascade);
    }
}
