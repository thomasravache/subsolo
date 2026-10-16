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

    public static IResult Unauthorized() =>
        Problem(StatusCodes.Status401Unauthorized, AuthRequired, "É preciso entrar para acessar este recurso.");

    public static IResult Forbidden() =>
        Problem(StatusCodes.Status403Forbidden, AuthForbidden, "Você não tem permissão para acessar este recurso.");

    public static IResult CsrfRejected() =>
        Problem(StatusCodes.Status400BadRequest, CsrfInvalid, "O token anti-forgery está ausente ou é inválido.");

    public static IResult TooManyRequests() =>
        Problem(StatusCodes.Status429TooManyRequests, RateLimited, "Muitas requisições. Tente de novo mais tarde.");

    private static IResult Problem(int status, string code, string detail) =>
        Results.Problem(
            detail: detail,
            statusCode: status,
            title: code,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { [ApiResults.CodeExtension] = code });
}
