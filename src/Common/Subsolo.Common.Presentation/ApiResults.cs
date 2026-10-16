using Microsoft.AspNetCore.Http;
using Subsolo.Common.Domain;

namespace Subsolo.Common.Presentation;

/// <summary>
/// SPEC-0007 — converte um <see cref="Result"/> com falha em ProblemDetails (RFC 9457): Validation 400 |
/// NotFound 404 | Conflict 409 | Unauthorized 401 | Forbidden 403 | Failure 500, com o código do erro em
/// <c>extensions.code</c>.
/// </summary>
public static class ApiResults
{
    public static IResult Problem(Result result) => throw new NotImplementedException();
}
