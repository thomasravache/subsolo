using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Common.Application.Decorators;

/// <summary>
/// SPEC-0007 — registra o nome do comando/consulta e o resultado (sucesso ou o código do erro). Nunca registra o
/// payload (dados pessoais, ADR-0008).
/// </summary>
public static partial class LoggingDecorator
{
    public sealed class CommandHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        ILogger<CommandHandler<TCommand>> logger)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            var name = typeof(TCommand).Name;
            LogProcessing(logger, "comando", name);
            var result = await innerHandler.Handle(command, cancellationToken);
            LogOutcome(logger, "comando", name, result);
            return result;
        }
    }

    public sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        ILogger<CommandHandler<TCommand, TResponse>> logger)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            var name = typeof(TCommand).Name;
            LogProcessing(logger, "comando", name);
            var result = await innerHandler.Handle(command, cancellationToken);
            LogOutcome(logger, "comando", name, result);
            return result;
        }
    }

    public sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        ILogger<QueryHandler<TQuery, TResponse>> logger)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            var name = typeof(TQuery).Name;
            LogProcessing(logger, "consulta", name);
            var result = await innerHandler.Handle(query, cancellationToken);
            LogOutcome(logger, "consulta", name, result);
            return result;
        }
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
