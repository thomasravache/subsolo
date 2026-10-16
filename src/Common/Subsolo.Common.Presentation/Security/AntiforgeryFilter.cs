using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

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

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var httpContext = context.HttpContext;
        if (!IsMutation(httpContext.Request.Method))
        {
            return await next(context);
        }

        if (string.IsNullOrWhiteSpace(httpContext.Request.Headers[HeaderName])
            || !await httpContext.RequestServices.GetRequiredService<IAntiforgery>().IsRequestValidAsync(httpContext))
        {
            SecurityLog.Rejected(httpContext, SecurityProblems.CsrfInvalid);
            return SecurityProblems.CsrfRejected();
        }

        return await next(context);
    }

    private static bool IsMutation(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
}
