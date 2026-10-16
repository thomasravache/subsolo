using System.Text.Json;

namespace Subsolo.Common.Infrastructure.Messaging;

/// <summary>SPEC-0007 — serialização dos eventos guardados na outbox e na inbox.</summary>
internal static class MessageSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General);

    /// <summary>Nome estável do tipo do evento, guardado na coluna <c>type</c>.</summary>
    public static string TypeName(Type type) => type.FullName ?? type.Name;

    public static string Serialize(object message) => JsonSerializer.Serialize(message, message.GetType(), Options);

    public static object Deserialize(string content, Type type) =>
        JsonSerializer.Deserialize(content, type, Options)
        ?? throw new InvalidOperationException($"Mensagem vazia para o tipo {type.FullName}.");
}
