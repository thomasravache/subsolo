using OpenTelemetry.Logs;

namespace Subsolo.Observability.UnitTests;

/// <summary>
/// O que o exportador OTLP leria de um <see cref="LogRecord"/>: atributos (com os itens dos escopos, que o OTLP
/// exporta como atributos), mensagem formatada, corpo e a exceção (<c>exception.message</c>,
/// <c>exception.stacktrace</c> = <see cref="Exception.ToString"/>).
/// </summary>
internal static class ExportedLog
{
    /// <summary>Atributos do registro seguidos dos itens de todos os escopos.</summary>
    public static List<KeyValuePair<string, object?>> AttributesAndScopes(LogRecord record)
    {
        var items = new List<KeyValuePair<string, object?>>(record.Attributes ?? []);
        record.ForEachScope(
            static (scope, list) =>
            {
                foreach (var item in scope)
                {
                    list.Add(item);
                }
            },
            items);
        return items;
    }

    /// <summary>Último valor (como texto) de cada chave dos atributos e escopos.</summary>
    public static Dictionary<string, string?> Fields(LogRecord record) =>
        AttributesAndScopes(record)
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Value?.ToString(), StringComparer.Ordinal);

    /// <summary>Todo o texto exportado do registro, numa só string.</summary>
    public static string Text(LogRecord record) =>
        string.Join(
            "\n",
            new[]
                {
                    record.FormattedMessage,
                    record.Body,
                    record.Exception?.GetType().FullName,
                    record.Exception?.Message,
                    record.Exception?.ToString(),
                }
                .Concat(AttributesAndScopes(record).Select(item => $"{item.Key}={item.Value}")));
}
