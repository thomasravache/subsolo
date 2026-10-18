using Subsolo.Common.Application.Messaging;
using Subsolo.Modules.Users.Application.Features.GetMe;

namespace Subsolo.Modules.Users.Application.Features.UpdateDisplayName;

/// <summary>
/// SPEC-0019 — <c>PUT /api/v1/me</c>: muda o nome de exibição de <paramref name="UserId"/> (sem espaços nas pontas, 1 a
/// 60 caracteres; vazio ou <see langword="null"/> remove o nome) e devolve o mesmo corpo do <c>GET /me</c>.
/// </summary>
public sealed record UpdateDisplayNameCommand(Guid UserId, string? DisplayName) : ICommand<MeResponse>;
