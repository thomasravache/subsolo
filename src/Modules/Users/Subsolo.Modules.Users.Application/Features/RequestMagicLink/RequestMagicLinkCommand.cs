using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Users.Application.Features.RequestMagicLink;

/// <summary>
/// SPEC-0016 — pedido de link mágico (<c>POST /api/v1/auth/magic-link</c>): e-mail (até 254) e o caminho de volta
/// opcional. Sucesso é igual exista a conta ou não (e também acima do limite por e-mail, sem envio).
/// </summary>
public sealed record RequestMagicLinkCommand(string Email, string? ReturnTo) : ICommand;
