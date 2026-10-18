using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Events.Application.Features.CleanupFlyers;

/// <summary>
/// SPEC-0022 — limpeza periódica: apaga os flyers não ligados a show criados há mais de 24 h (as três variantes e a
/// linha). Disparado pela tarefa periódica do módulo.
/// </summary>
public sealed record CleanupFlyersCommand : ICommand;
