using FluentValidation;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Common.Application.Decorators;

/// <summary>
/// SPEC-0007 — valida o comando com os validadores FluentValidation registrados antes de chamar o handler. Comando
/// inválido vira <see cref="Error"/> do tipo <see cref="ErrorType.Validation"/> com a lista de campos
/// (<see cref="Error.ValidationErrors"/>), e o handler não é chamado.
/// </summary>
public static class ValidationDecorator
{
    public sealed class CommandHandler<TCommand> : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public CommandHandler(
            ICommandHandler<TCommand> innerHandler,
            IEnumerable<IValidator<TCommand>> validators)
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
            IEnumerable<IValidator<TCommand>> validators)
        {
            throw new NotImplementedException();
        }

        public Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}
