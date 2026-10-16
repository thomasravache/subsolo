using FluentValidation;
using FluentValidation.Results;
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
    /// <summary>Código do erro de validação de comando.</summary>
    public const string ErrorCode = "Validation.Failed";

    public sealed class CommandHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        IEnumerable<IValidator<TCommand>> validators)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            var error = await ValidateAsync(command, validators, cancellationToken);
            return error is null
                ? await innerHandler.Handle(command, cancellationToken)
                : Result.Failure(error);
        }
    }

    public sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        IEnumerable<IValidator<TCommand>> validators)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            var error = await ValidateAsync(command, validators, cancellationToken);
            return error is null
                ? await innerHandler.Handle(command, cancellationToken)
                : Result.Failure<TResponse>(error);
        }
    }

    /// <summary>Roda todos os validadores e devolve o erro de validação, ou <see langword="null"/> se o comando é válido.</summary>
    private static async Task<Error?> ValidateAsync<TCommand>(
        TCommand command,
        IEnumerable<IValidator<TCommand>> validators,
        CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();
        var context = new ValidationContext<TCommand>(command);
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count == 0)
        {
            return null;
        }

        var fields = failures
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        return Error.Validation(ErrorCode, "Um ou mais campos são inválidos.", fields);
    }
}
