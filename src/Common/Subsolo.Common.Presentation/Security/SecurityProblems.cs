using Microsoft.AspNetCore.Http;

namespace Subsolo.Common.Presentation.Security;

/// <summary>
/// SPEC-0010 — respostas de segurança em ProblemDetails (RFC 9457), no mesmo formato de <see cref="ApiResults"/>:
/// o código vai em <c>title</c> e na extensão <see cref="ApiResults.CodeExtension"/>.
/// </summary>
public static class SecurityProblems
{
    /// <summary>401: o endpoint exige um usuário autenticado.</summary>
    public const string AuthRequired = "auth.required";

    /// <summary>403: o usuário não tem o papel que a política exige.</summary>
    public const string AuthForbidden = "auth.forbidden";

    /// <summary>400: mutação sem o cabeçalho anti-forgery válido.</summary>
    public const string CsrfInvalid = "security.csrf_invalid";

    /// <summary>429: limite de requisições da política excedido.</summary>
    public const string RateLimited = "security.rate_limited";

    /// <summary>503: a sessão não pôde ser validada porque o banco não respondeu (SPEC-0014).</summary>
    public const string ServiceUnavailable = "service.unavailable";

    public static IResult Unauthorized() =>
        Problem(StatusCodes.Status401Unauthorized, AuthRequired, "É preciso entrar para acessar este recurso.");

    public static IResult Forbidden() =>
        Problem(StatusCodes.Status403Forbidden, AuthForbidden, "Você não tem permissão para acessar este recurso.");

    public static IResult CsrfRejected() =>
        Problem(StatusCodes.Status400BadRequest, CsrfInvalid, "O token anti-forgery está ausente ou é inválido.");

    public static IResult TooManyRequests() =>
        Problem(StatusCodes.Status429TooManyRequests, RateLimited, "Muitas requisições. Tente de novo mais tarde.");

    public static IResult Unavailable() =>
        Problem(StatusCodes.Status503ServiceUnavailable, ServiceUnavailable, "Serviço temporariamente indisponível. Tente de novo em instantes.");

    private static IResult Problem(int status, string code, string detail) =>
        Results.Problem(
            detail: detail,
            statusCode: status,
            title: code,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { [ApiResults.CodeExtension] = code });
}
