using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Site.Application.Features.RemoveSupporter;

/// <summary>
/// SPEC-0028 — <c>DELETE /api/v1/admin/supporters/{id}</c>: remove o apoiador <paramref name="Id"/>.
/// <paramref name="AdminId"/>: quem remove, para o log de auditoria.
/// </summary>
public sealed record RemoveSupporterCommand(Guid Id, Guid? AdminId = null) : ICommand;
