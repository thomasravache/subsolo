namespace Subsolo.Modules.Events.Application.Features.ListStyles;

/// <summary>SPEC-0017 — estilo na lista pública (<c>GET /api/v1/styles</c>) e no painel do admin.</summary>
public sealed record StyleResponse(string Slug, string Name);
