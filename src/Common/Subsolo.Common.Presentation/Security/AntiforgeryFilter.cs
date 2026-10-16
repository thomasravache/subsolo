using Microsoft.AspNetCore.Http;

namespace Subsolo.Common.Presentation.Security;

/// <summary>
/// SPEC-0010 — filtro de anti-forgery das mutações: POST, PUT, PATCH e DELETE exigem o cabeçalho
/// <see cref="HeaderName"/> com o token emitido em <c>GET /api/v1/antiforgery</c>; sem ele (ou com token inválido),
/// 400 ProblemDetails <c>code=security.csrf_invalid</c>. Os demais métodos seguem sem verificação. Os serviços
/// (ex.: <c>IAntiforgery</c>) vêm de <c>HttpContext.RequestServices</c>.
/// </summary>
public sealed class AntiforgeryFilter : IEndpointFilter
{
    /// <summary>Cabeçalho que carrega o token anti-forgery nas mutações.</summary>
    public const string HeaderName = "X-CSRF";

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        throw new NotImplementedException("SPEC-0010: scaffold — implementar o filtro de anti-forgery.");
}
