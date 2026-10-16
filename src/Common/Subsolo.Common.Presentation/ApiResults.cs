using Microsoft.AspNetCore.Http;
using Subsolo.Common.Domain;

namespace Subsolo.Common.Presentation;

/// <summary>
/// SPEC-0007 — converte um <see cref="Result"/> com falha em ProblemDetails (RFC 9457): Validation 400 |
/// NotFound 404 | Conflict 409 | Unauthorized 401 | Forbidden 403 | Failure 500, com o código do erro em
/// <c>extensions.code</c>. Em erros de validação, os campos vão em <c>errors</c> (campo → mensagens).
/// </summary>
public static class ApiResults
{
    /// <summary>Nome da extensão do ProblemDetails que carrega o <see cref="Error.Code"/>.</summary>
    public const string CodeExtension = "code";

    public static IResult Problem(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Um resultado de sucesso não vira ProblemDetails.");
        }

        var error = result.Error;
        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { [CodeExtension] = error.Code };
        var status = StatusCode(error.Type);

        return error.Type == ErrorType.Validation
            ? Results.ValidationProblem(
                error.ValidationErrors,
                detail: error.Description,
                statusCode: status,
                title: error.Code,
                extensions: extensions)
            : Results.Problem(
                detail: error.Description,
                statusCode: status,
                title: error.Code,
                extensions: extensions);
    }

    private static int StatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError,
    };
}
