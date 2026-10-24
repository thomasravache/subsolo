using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Moderation.Application.Features.KeepShow;

/// <summary>
/// SPEC-0033 — <c>POST /api/v1/admin/reports/{showId}/keep</c>: o admin <paramref name="AdminId"/> mantém o show
/// <paramref name="ShowId"/>; todas as denúncias pendentes dele são arquivadas (<c>kept</c>).
/// </summary>
public sealed record KeepShowCommand(Guid ShowId, Guid AdminId) : ICommand;
