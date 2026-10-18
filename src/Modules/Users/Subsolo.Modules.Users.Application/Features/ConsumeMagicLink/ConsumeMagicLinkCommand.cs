using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Users.Application.Features.ConsumeMagicLink;

/// <summary>SPEC-0016 — troca do token do link pela sessão (<c>POST /api/v1/auth/magic-link/consume</c>).</summary>
public sealed record ConsumeMagicLinkCommand(string Token) : ICommand<ConsumeMagicLinkResponse>;

/// <summary>SPEC-0016 — corpo do 200 do consumo: o caminho de volta saneado (padrão <c>/</c>).</summary>
public sealed record ConsumeMagicLinkResponse(string ReturnTo);
