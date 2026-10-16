using Microsoft.Extensions.Logging;

namespace Subsolo.ServiceDefaults.Redaction;

/// <summary>
/// SPEC-0009 (ADR-0008) — <see cref="ILoggerFactory"/> do host: envolve a fábrica padrão e mascara dados pessoais
/// e segredos no estado, na mensagem e nos escopos de todo log, antes de qualquer provedor (OpenTelemetry/OTLP,
/// console) recebê-lo.
/// </summary>
internal sealed class RedactingLoggerFactory(ILoggerFactory inner) : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName) => new RedactingLogger(inner.CreateLogger(categoryName));

    public void AddProvider(ILoggerProvider provider) => inner.AddProvider(provider);

    public void Dispose() => inner.Dispose();

    private sealed class RedactingLogger(ILogger inner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            state is IReadOnlyList<KeyValuePair<string, object?>> values &&
            RedactedLogState.From(values, state.ToString) is { } redacted
                ? inner.BeginScope(redacted)
                : inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (!inner.IsEnabled(logLevel))
            {
                return;
            }

            if (state is IReadOnlyList<KeyValuePair<string, object?>> values &&
                RedactedLogState.From(values, () => formatter(state, exception)) is { } redacted)
            {
                inner.Log(logLevel, eventId, redacted, exception, static (s, _) => s.ToString());
                return;
            }

            inner.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
