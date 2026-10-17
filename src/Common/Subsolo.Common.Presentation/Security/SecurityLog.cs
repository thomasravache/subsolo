using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Subsolo.Common.Presentation.Security;

/// <summary>
/// SPEC-0010 — registro das recusas de segurança (401, 403, 429 e CSRF) com o código e o método, sem dados pessoais
/// (nem IP, nem usuário, nem caminho): a contagem por código aparece no Seq.
/// </summary>
public static partial class SecurityLog
{
    /// <summary>Categoria dos registros de recusa.</summary>
    public const string Category = "Subsolo.Security";

    public static void Rejected(HttpContext httpContext, string code)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var logger = httpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger(Category);
        if (logger is not null)
        {
            Rejected(logger, code, httpContext.Request.Method);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Requisição recusada pela segurança: {SecurityCode} ({Method})")]
    private static partial void Rejected(ILogger logger, string securityCode, string method);
}
