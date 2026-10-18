using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Profiles.Application.Features.ListProfileRequests;

/// <summary>SPEC-0021 — <c>GET /api/v1/admin/profile-requests</c>: a fila do admin, pendentes e mais antigos primeiro.</summary>
public sealed record ListProfileRequestsQuery : IQuery<IReadOnlyList<ProfileRequestQueueItem>>;

/// <summary>
/// SPEC-0021 — um pedido na fila do admin: os dados do perfil (o pedido novo ou o perfil reivindicado), o contato ou a
/// prova e o e-mail de quem pediu (só aqui, ADR-0008) e, na reivindicação, o perfil.
/// </summary>
public sealed record ProfileRequestQueueItem(
    Guid RequestId,
    string Kind,
    string Type,
    string Name,
    string City,
    IReadOnlyList<string> Styles,
    ProfileLinksResponse Links,
    string ContactOrProof,
    string? RequesterEmail,
    DateTimeOffset CreatedAtUtc,
    Guid? ProfileId);

/// <summary>SPEC-0021 — links do perfil (<see langword="null"/> = não informado).</summary>
public sealed record ProfileLinksResponse(string? Instagram, string? Bandcamp, string? MetalArchives);

/// <summary>SPEC-0021 — um pedido pendente lido do banco, ainda sem o e-mail de quem pediu.</summary>
internal sealed record PendingProfileRequest(
    Guid RequestId,
    string Kind,
    Guid RequesterUserId,
    string Type,
    string Name,
    string City,
    IReadOnlyList<string> Styles,
    ProfileLinksResponse Links,
    string ContactOrProof,
    DateTimeOffset CreatedAtUtc,
    Guid? ProfileId);
