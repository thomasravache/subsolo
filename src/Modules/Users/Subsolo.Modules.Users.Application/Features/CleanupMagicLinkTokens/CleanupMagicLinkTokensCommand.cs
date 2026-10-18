using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Users.Application.Features.CleanupMagicLinkTokens;

/// <summary>
/// SPEC-0016 — limpeza periódica (ADR-0008): apaga os links usados ou expirados há mais de 24 h. Disparado pela
/// tarefa periódica do módulo.
/// </summary>
public sealed record CleanupMagicLinkTokensCommand : ICommand;
