using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Common.Application.Decorators;

/// <summary>
/// SPEC-0007 — registra o nome do comando/consulta e o resultado (sucesso ou o código do erro). Nunca registra o
/// payload (dados pessoais, ADR-0008).
/// </summary>
public static class LoggingDecorator
{
    public sealed class CommandHandler<TCommand> : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public CommandHandler(
            ICommandHandler<TCommand> innerHandler,
            ILogger<CommandHandler<TCommand>> logger)
        {
            throw new NotImplementedException();
        }

        public Task<Result> Handle(TCommand command, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    public sealed class CommandHandler<TCommand, TResponse> : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public CommandHandler(
            ICommandHandler<TCommand, TResponse> innerHandler,
            ILogger<CommandHandler<TCommand, TResponse>> logger)
        {
            throw new NotImplementedException();
        }

        public Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    public sealed class QueryHandler<TQuery, TResponse> : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public QueryHandler(
            IQueryHandler<TQuery, TResponse> innerHandler,
            ILogger<QueryHandler<TQuery, TResponse>> logger)
        {
            throw new NotImplementedException();
        }

        public Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}
