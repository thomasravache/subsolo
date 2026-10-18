using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Events.Application.Features.ListStyles;

namespace Subsolo.Modules.Events.Application.Features.RenameStyle;

/// <summary>
/// SPEC-0017 — <c>PUT /api/v1/admin/styles/{slug}</c>: novo nome (2 a 30 caracteres); o slug não muda.
/// <paramref name="AdminId"/>: quem renomeia, para o log de auditoria.
/// </summary>
public sealed record RenameStyleCommand(string Slug, string Name, Guid? AdminId = null) : ICommand<StyleResponse>;
