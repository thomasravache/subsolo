using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Moderation.Application.Features.HideShow;

/// <summary>
/// SPEC-0033 — <c>POST /api/v1/admin/reports/{showId}/hide</c>: o admin <paramref name="AdminId"/> oculta o show
/// <paramref name="ShowId"/>, com a nota interna opcional (até 200 caracteres; nunca sai pela API nem pelo export).
/// </summary>
public sealed record HideShowCommand(Guid ShowId, Guid AdminId, string? Note) : ICommand;
