using Subsolo.Common.Infrastructure.Messaging;

namespace Subsolo.Common.Infrastructure.Outbox;

/// <summary>SPEC-0007 — evento de domínio gravado na outbox do módulo, na mesma transação da entidade.</summary>
internal sealed class OutboxMessage : ModuleMessage;

/// <summary>SPEC-0007 — handler de evento de domínio que já processou uma mensagem da outbox.</summary>
internal sealed class OutboxMessageConsumer : ModuleMessageConsumer;
