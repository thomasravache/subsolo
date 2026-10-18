using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Profiles.Application.Features.Shared;

namespace Subsolo.Modules.Profiles.Application.Features.RequestProfile;

/// <summary>
/// SPEC-0021 — <c>POST /api/v1/profiles/requests</c>: o fã <paramref name="UserId"/> pede o perfil (tipo
/// <c>band</c> | <c>venue</c> | <c>producer</c>, nome 2–80, cidade, estilos só de banda 1–5, links <c>https</c> dos
/// domínios oficiais) com o contato para a aprovação (3–200).
/// </summary>
public sealed record RequestProfileCommand(
    Guid UserId,
    string? Type,
    string? Name,
    string? City,
    IReadOnlyList<string>? Styles,
    ProfileLinksInput? Links,
    string? Contact) : ICommand<ProfileRequestCreated>, IProfileFields;

/// <summary>SPEC-0021 — links como chegaram (Instagram, Bandcamp, Metal Archives); vazios = não informados.</summary>
public sealed record ProfileLinksInput(string? Instagram, string? Bandcamp, string? MetalArchives);

/// <summary>SPEC-0021 — o pedido criado (201): id e situação (<c>pending</c>).</summary>
public sealed record ProfileRequestCreated(Guid RequestId, string Status);
