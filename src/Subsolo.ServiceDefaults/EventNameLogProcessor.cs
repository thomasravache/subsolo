using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Subsolo.ServiceDefaults;

/// <summary>
/// SPEC-0009 — grava o nome do evento de log (<c>EventId.Name</c>) como o atributo <see cref="AttributeName"/>,
/// antes dos exportadores. O exportador OTLP manda o nome no campo <c>event_name</c> do registro, que o Seq
/// (2025.2) não expõe para consulta; com o atributo, o alerta "Falha de job" filtra por
/// <c>event.name = 'OutboxProcessingFailed'</c>.
/// </summary>
internal sealed class EventNameLogProcessor : BaseProcessor<LogRecord>
{
    public const string AttributeName = "event.name";

    public override void OnEnd(LogRecord data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.EventId.Name is not { Length: > 0 } name)
        {
            return;
        }

        var attributes = data.Attributes ?? [];
        foreach (var (key, _) in attributes)
        {
            if (key == AttributeName)
            {
                return;
            }
        }

        data.Attributes = [.. attributes, new KeyValuePair<string, object?>(AttributeName, name)];
    }
}
