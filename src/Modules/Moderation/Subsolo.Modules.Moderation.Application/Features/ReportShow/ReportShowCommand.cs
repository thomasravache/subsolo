using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Moderation.Application.Features.ReportShow;

/// <summary>
/// SPEC-0033 — <c>POST /api/v1/shows/{id}/reports</c>: o fã <paramref name="UserId"/> denuncia o show
/// <paramref name="ShowId"/> com o motivo (<c>fake | cancelled_unannounced | wrong_info | other</c>) e o detalhe opcional
/// (até 500 caracteres; obrigatório em <c>other</c>).
/// </summary>
public sealed record ReportShowCommand(Guid UserId, Guid ShowId, string? Reason, string? Details) : ICommand<ReportCreated>;

/// <summary>SPEC-0033 — corpo do 201 da denúncia: o id dela.</summary>
public sealed record ReportCreated(Guid Id);
