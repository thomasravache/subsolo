using System.Diagnostics;

namespace Subsolo.Common.Infrastructure.Messaging;

/// <summary>
/// SPEC-0007 — <see cref="ActivitySource"/> do processamento da outbox e da inbox. O ServiceDefaults assina todas
/// as fontes <c>Subsolo.*</c> no tracing (dashboard do Aspire).
/// </summary>
public static class MessagingTelemetry
{
    public const string SourceName = "Subsolo.Messaging";

    /// <summary>Tag com o nome do módulo dono da outbox/inbox.</summary>
    public const string ModuleTag = "subsolo.module";

    /// <summary>Tag com o Id da mensagem (o Id do evento).</summary>
    public const string MessageIdTag = "subsolo.message.id";

    internal static readonly ActivitySource Source = new(SourceName);
}
