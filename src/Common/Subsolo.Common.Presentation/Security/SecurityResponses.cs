using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Subsolo.Common.Presentation.Security;

/// <summary>
/// SPEC-0014 — o código estável de uma resposta de erro documentada (ex.: <c>401 auth.required</c>), que o host põe na
/// descrição da resposta no OpenAPI para o cliente gerado saber que código esperar.
/// </summary>
public sealed record ProblemCodeMetadata(int StatusCode, string Code);

/// <summary>SPEC-0014 — <c>.ProducesProblemCode(status, código)</c>: resposta ProblemDetails com o código documentado.</summary>
public static class SecurityResponses
{
    public static RouteHandlerBuilder ProducesProblemCode(this RouteHandlerBuilder builder, int statusCode, string code)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.ProducesProblem(statusCode).WithMetadata(new ProblemCodeMetadata(statusCode, code));
    }
}
