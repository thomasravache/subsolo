using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Events.Application.Features.ListStyles;

/// <summary>SPEC-0017 — <c>GET /api/v1/styles</c>: os estilos, ordenados pelo nome.</summary>
public sealed record ListStylesQuery : IQuery<IReadOnlyList<StyleResponse>>;
