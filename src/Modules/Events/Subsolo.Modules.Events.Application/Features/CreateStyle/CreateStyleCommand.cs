using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Events.Application.Features.ListStyles;

namespace Subsolo.Modules.Events.Application.Features.CreateStyle;

/// <summary>
/// SPEC-0017 — <c>POST /api/v1/admin/styles</c>: estilo novo com nome de 2 a 30 caracteres (slug gerado do nome).
/// <paramref name="AdminId"/>: quem cria, para o log de auditoria.
/// </summary>
public sealed record CreateStyleCommand(string Name, Guid? AdminId = null) : ICommand<StyleResponse>;
