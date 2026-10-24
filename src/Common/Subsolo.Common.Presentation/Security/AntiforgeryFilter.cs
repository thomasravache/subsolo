using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Subsolo.Common.Presentation.Security;

/// <summary>
/// SPEC-0010 — filtro de anti-forgery das mutações: POST, PUT, PATCH e DELETE exigem o cabeçalho
/// <see cref="HeaderName"/> com o token emitido em <c>GET /api/v1/antiforgery</c>; sem ele (ou com token inválido),
/// 400 ProblemDetails <c>code=security.csrf_invalid</c>. Os demais métodos seguem sem verificação. Os serviços
/// (ex.: <c>IAntiforgery</c>) vêm de <c>HttpContext.RequestServices</c>. SPEC-0031 — um endpoint marcado com
/// <see cref="CsrfExemptMetadata"/> (<c>.ExemptFromCsrf(motivo)</c>) passa sem o cabeçalho: só quando outra credencial da
/// própria requisição autoriza a mutação (o token assinado do descadastro em um clique, RFC 8058).
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
        if (!IsMutation(httpContext.Request.Method)
            || httpContext.GetEndpoint()?.Metadata.GetMetadata<CsrfExemptMetadata>() is not null)
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

/// <summary>
/// SPEC-0031 — o endpoint dispensa o <c>X-CSRF</c> porque a mutação é autorizada por outra credencial da própria
/// requisição (<see cref="Reason"/> diz qual); sem cookie de sessão envolvido, não há o que forjar.
/// </summary>
public sealed record CsrfExemptMetadata(string Reason);

/// <summary>SPEC-0031 — <c>.ExemptFromCsrf(motivo)</c>: marca o endpoint com <see cref="CsrfExemptMetadata"/>.</summary>
public static class CsrfExemptionExtensions
{
    public static TBuilder ExemptFromCsrf<TBuilder>(this TBuilder builder, string reason)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return builder.WithMetadata(new CsrfExemptMetadata(reason));
    }
}
