using System.Diagnostics;

namespace Subsolo.Common.Application.Diagnostics;

/// <summary>
/// SPEC-0007 — <see cref="ActivitySource"/> dos comandos e consultas, usada pelo <c>LoggingDecorator</c>. O
/// ServiceDefaults assina todas as fontes <c>Subsolo.*</c> no tracing (dashboard do Aspire).
/// </summary>
public static class ApplicationTelemetry
{
    public const string SourceName = "Subsolo.Application";

    /// <summary>Tag com o tipo da mensagem (<c>comando</c> ou <c>consulta</c>).</summary>
    public const string RequestKindTag = "subsolo.request.kind";

    /// <summary>Tag com o código do <c>Error</c> de um resultado de falha.</summary>
    public const string ErrorCodeTag = "subsolo.error.code";

    /// <summary>Tag com o <c>ErrorType</c> de um resultado de falha (convenção semântica do OpenTelemetry).</summary>
    public const string ErrorTypeTag = "error.type";

    internal static readonly ActivitySource Source = new(SourceName);
}
