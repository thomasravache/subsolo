using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Subsolo.Common.IntegrationTests.Observability;

/// <summary>Um log capturado: categoria, nível, <see cref="EventId"/>, exceção e mensagem formatada.</summary>
internal sealed record CapturedLog(string Category, LogLevel Level, EventId EventId, Exception? Exception, string Message);

/// <summary>
/// Provedor de log que guarda tudo o que recebe; adicionado à <see cref="ILoggerFactory"/> do host depois de
/// construído (<see cref="ILoggerFactory.AddProvider"/>), como mais um destino dos logs.
/// </summary>
internal sealed class CapturedLogs : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLog> _logs = new();

    public IReadOnlyList<CapturedLog> All => [.. _logs];

    public static CapturedLogs AttachTo(IServiceProvider services)
    {
        var captured = new CapturedLogs();
        services.GetRequiredService<ILoggerFactory>().AddProvider(captured);
        return captured;
    }

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _logs);

    public void Dispose()
    {
    }

    private sealed class Logger(string category, ConcurrentQueue<CapturedLog> logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            logs.Enqueue(new CapturedLog(category, logLevel, eventId, exception, formatter(state, exception)));
    }
}
