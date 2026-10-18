using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Events.Application.Features.ListStyles;

namespace Subsolo.Modules.Events.Application.Features.CreateStyle;

/// <summary>SPEC-0017 — <c>POST /api/v1/admin/styles</c>: estilo novo com nome de 2 a 30 caracteres (slug gerado do nome).</summary>
public sealed record CreateStyleCommand(string Name) : ICommand<StyleResponse>;
