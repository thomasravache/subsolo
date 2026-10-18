namespace Subsolo.Modules.Profiles.Domain.Profiles;

/// <summary>
/// SPEC-0021 — os dados de um perfil já validados: tipo (<see cref="ProfileTypes"/>), nome (2–80, sem espaços nas
/// pontas), slug gerado do nome (único por tipo, imutável), cidade (slug existente), estilos (slugs existentes; só
/// banda, 1–5) e links. É o conteúdo de um pedido novo e de um perfil criado pelo admin.
/// </summary>
public sealed record ProfileDraft(
    string Type,
    string Name,
    string Slug,
    string City,
    IReadOnlyList<string> Styles,
    ProfileLinks Links);
