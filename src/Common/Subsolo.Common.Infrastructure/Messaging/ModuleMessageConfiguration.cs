using Microsoft.EntityFrameworkCore;
using Subsolo.Common.Infrastructure.Inbox;
using Subsolo.Common.Infrastructure.Outbox;

namespace Subsolo.Common.Infrastructure.Messaging;

/// <summary>SPEC-0007 — mapeamento das tabelas de outbox e inbox no schema do módulo (nomes em snake_case).</summary>
internal static class ModuleMessageConfiguration
{
    public static void ApplyMessaging(this ModelBuilder modelBuilder)
    {
        Message<OutboxMessage>(modelBuilder, "outbox_messages");
        Consumer<OutboxMessageConsumer, OutboxMessage>(modelBuilder, "outbox_message_consumers", "outbox_message_id");
        Message<InboxMessage>(modelBuilder, "inbox_messages");
        Consumer<InboxMessageConsumer, InboxMessage>(modelBuilder, "inbox_message_consumers", "inbox_message_id");
    }

    private static void Message<TMessage>(ModelBuilder modelBuilder, string table)
        where TMessage : ModuleMessage =>
        modelBuilder.Entity<TMessage>(message =>
        {
            message.ToTable(table);
            message.HasKey(m => m.Id);
            message.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
            message.Property(m => m.Type).HasColumnName("type").HasMaxLength(500);
            message.Property(m => m.Content).HasColumnName("content").HasColumnType("jsonb");
            message.Property(m => m.OccurredOnUtc).HasColumnName("occurred_on_utc");
            message.Property(m => m.ProcessedOnUtc).HasColumnName("processed_on_utc");
            message.Property(m => m.Attempts).HasColumnName("attempts");
            message.Property(m => m.NextAttemptOnUtc).HasColumnName("next_attempt_on_utc");
            message.Property(m => m.Error).HasColumnName("error").HasMaxLength(2000);
            message.HasIndex(m => m.OccurredOnUtc)
                .HasDatabaseName($"ix_{table}_pending")
                .HasFilter("processed_on_utc IS NULL");
        });

    private static void Consumer<TConsumer, TMessage>(ModelBuilder modelBuilder, string table, string messageIdColumn)
        where TConsumer : ModuleMessageConsumer
        where TMessage : ModuleMessage =>
        modelBuilder.Entity<TConsumer>(consumer =>
        {
            consumer.ToTable(table);
            consumer.HasKey(c => new { c.MessageId, c.Name });
            consumer.Property(c => c.MessageId).HasColumnName(messageIdColumn);
            consumer.Property(c => c.Name).HasColumnName("name").HasMaxLength(500);
            consumer.HasOne<TMessage>().WithMany().HasForeignKey(c => c.MessageId).OnDelete(DeleteBehavior.Cascade);
        });
}
