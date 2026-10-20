using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Site.Application.Features.ListSupporters;

/// <summary>SPEC-0028 — <c>GET /api/v1/supporters</c>: os apoiadores, na ordem de cadastro.</summary>
public sealed record ListSupportersQuery : IQuery<IReadOnlyList<SupporterResponse>>;

/// <summary>SPEC-0028 — um apoiador da lista pública: id, nome e link <c>https</c> (ou nenhum).</summary>
public sealed record SupporterResponse(Guid Id, string Name, string? Url);
