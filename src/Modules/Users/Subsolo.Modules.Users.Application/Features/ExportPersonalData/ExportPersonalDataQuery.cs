using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Users.Application.Features.ExportPersonalData;

/// <summary>SPEC-0019 — <c>GET /api/v1/me/export</c>: tudo o que o Subsolo guarda de <paramref name="UserId"/>.</summary>
public sealed record ExportPersonalDataQuery(Guid UserId) : IQuery<PersonalDataExport>;

/// <summary>
/// SPEC-0019 — o arquivo do export (§6): quando foi gerado, a conta, as sessões (nunca o hash do token) e as seções de
/// cada <c>IPersonalDataSection</c> registrada (pelo nome; as que devolvem <see langword="null"/> ficam de fora).
/// </summary>
public sealed record PersonalDataExport(
    DateTimeOffset ExportedAtUtc,
    PersonalDataAccount Account,
    IReadOnlyList<PersonalDataSession> Sessions,
    IReadOnlyDictionary<string, object> Sections);

/// <summary>SPEC-0019 — a conta no export.</summary>
public sealed record PersonalDataAccount(
    Guid Id,
    string Email,
    string? DisplayName,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAtUtc);

/// <summary>SPEC-0019 — uma sessão no export: datas e método (<c>magic_link</c> ou <c>google</c>), sem hash nem token.</summary>
public sealed record PersonalDataSession(
    DateTimeOffset CreatedAtUtc,
    string Method,
    DateTimeOffset LastSeenAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset AbsoluteExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc);
