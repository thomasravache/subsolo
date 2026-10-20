using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Site.Application.Features.AddSupporter;

/// <summary>
/// SPEC-0028 — <c>POST /api/v1/admin/supporters</c>: apoiador novo com nome de 2 a 60 caracteres e link <c>https</c>
/// opcional; devolve o id. <paramref name="AdminId"/>: quem cadastra, para o log de auditoria.
/// </summary>
public sealed record AddSupporterCommand(string Name, string? Url, Guid? AdminId = null) : ICommand<Guid>;
