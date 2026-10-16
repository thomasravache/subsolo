using Subsolo.Common.Infrastructure.Messaging;

namespace Subsolo.Common.Infrastructure.Inbox;

/// <summary>
/// SPEC-0007 — evento de integração recebido pelo módulo. O <see cref="ModuleMessage.Id"/> é o id do evento: a
/// mesma entrega repetida não gera uma segunda linha (inbox idempotente).
/// </summary>
internal sealed class InboxMessage : ModuleMessage;

/// <summary>SPEC-0007 — handler de evento de integração que já processou uma mensagem da inbox.</summary>
internal sealed class InboxMessageConsumer : ModuleMessageConsumer;
