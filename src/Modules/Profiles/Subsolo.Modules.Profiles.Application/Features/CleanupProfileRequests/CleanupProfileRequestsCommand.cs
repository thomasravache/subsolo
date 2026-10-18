using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Profiles.Application.Features.CleanupProfileRequests;

/// <summary>
/// SPEC-0021 — limpeza periódica (ADR-0008): apaga os pedidos decididos há mais de 90 dias, com o contato ou a prova.
/// Disparado pela tarefa periódica do módulo.
/// </summary>
public sealed record CleanupProfileRequestsCommand : ICommand;
