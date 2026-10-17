using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Users.Application.Features.GetMe;

/// <summary>SPEC-0014 — quem está logado: a conta do usuário da sessão validada.</summary>
public sealed record GetMeQuery(Guid UserId) : IQuery<MeResponse>;

/// <summary>SPEC-0014 — corpo de <c>GET /api/v1/me</c> (§6): id, e-mail, nome de exibição (ou nulo) e papéis.</summary>
public sealed record MeResponse(Guid Id, string Email, string? DisplayName, IReadOnlyList<string> Roles);
