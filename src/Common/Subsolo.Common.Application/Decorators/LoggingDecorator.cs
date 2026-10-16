using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Diagnostics;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Common.Application.Decorators;

/// <summary>
/// SPEC-0007 — registra o nome do comando/consulta e o resultado (sucesso ou o código do erro) no log e numa
/// atividade da fonte <see cref="ApplicationTelemetry.SourceName"/> (nome da mensagem; status <c>Ok</c> ou
/// <c>Error</c> com o código do erro em tag). Nunca registra o payload (dados pessoais, ADR-0008).
/// </summary>
public static partial class LoggingDecorator
{
    public sealed class CommandHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        ILogger<CommandHandler<TCommand>> logger)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public Task<Result> Handle(TCommand command, CancellationToken cancellationToken) =>
            TraceAsync(logger, "comando", typeof(TCommand).Name, () => innerHandler.Handle(command, cancellationToken));
    }

    public sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        ILogger<CommandHandler<TCommand, TResponse>> logger)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken) =>
            TraceAsync(logger, "comando", typeof(TCommand).Name, () => innerHandler.Handle(command, cancellationToken));
    }

    public sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        ILogger<QueryHandler<TQuery, TResponse>> logger)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken) =>
            TraceAsync(logger, "consulta", typeof(TQuery).Name, () => innerHandler.Handle(query, cancellationToken));
    }

    /// <summary>Executa o handler dentro de uma atividade com o nome da mensagem e registra o resultado.</summary>
    private static async Task<TResult> TraceAsync<TResult>(ILogger logger, string kind, string name, Func<Task<TResult>> handle)
        where TResult : Result
    {
        using var activity = ApplicationTelemetry.Source.StartActivity(name);
        activity?.SetTag(ApplicationTelemetry.RequestKindTag, kind);
        LogProcessing(logger, kind, name);
        try
        {
            var result = await handle();
            LogOutcome(logger, kind, name, result);
            RecordOutcome(activity, result);
            return result;
        }
        catch (Exception exception)
        {
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            throw;
        }
    }

    /// <summary>Status da atividade; na falha, o código e o tipo do erro (nunca a mensagem nem o payload).</summary>
    private static void RecordOutcome(Activity? activity, Result result)
    {
        if (activity is null)
        {
            return;
        }

        if (result.IsSuccess)
        {
            activity.SetStatus(ActivityStatusCode.Ok);
            return;
        }

        activity.SetTag(ApplicationTelemetry.ErrorCodeTag, result.Error.Code);
        activity.SetTag(ApplicationTelemetry.ErrorTypeTag, result.Error.Type.ToString());
        activity.SetStatus(ActivityStatusCode.Error, result.Error.Code);
    }

    /// <summary>Só o tipo da mensagem e o resultado: o payload nunca entra no log.</summary>
    private static void LogOutcome(ILogger logger, string kind, string name, Result result)
    {
        if (result.IsSuccess)
        {
            LogCompleted(logger, kind, name);
        }
        else
        {
            LogFailed(logger, kind, name, result.Error.Code, result.Error.Type);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Processando {RequestKind} {RequestName}")]
    private static partial void LogProcessing(ILogger logger, string requestKind, string requestName);

    [LoggerMessage(Level = LogLevel.Information, Message = "{RequestKind} {RequestName} concluído com sucesso")]
    private static partial void LogCompleted(ILogger logger, string requestKind, string requestName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{RequestKind} {RequestName} concluído com erro {ErrorCode} ({ErrorType})")]
    private static partial void LogFailed(ILogger logger, string requestKind, string requestName, string errorCode, ErrorType errorType);
}
