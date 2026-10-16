using FluentValidation;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Common.UnitTests.Doubles;

/// <summary>Comando de exemplo com um dado pessoal (e-mail) no payload.</summary>
public sealed record RegisterFan(string Name, string Email) : ICommand;

/// <summary>Comando de exemplo com valor de retorno.</summary>
public sealed record RegisterFanWithId(string Name, string Email) : ICommand<Guid>;

public sealed class RegisterFanValidator : AbstractValidator<RegisterFan>
{
    public RegisterFanValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
        RuleFor(c => c.Email).EmailAddress();
    }
}

public sealed class RegisterFanWithIdValidator : AbstractValidator<RegisterFanWithId>
{
    public RegisterFanWithIdValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
        RuleFor(c => c.Email).EmailAddress();
    }
}

/// <summary>Handler de exemplo que conta as chamadas e devolve o resultado configurado.</summary>
public sealed class RecordingHandler(Func<Result> outcome) : ICommandHandler<RegisterFan>
{
    public int Calls { get; private set; }

    public Task<Result> Handle(RegisterFan command, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(outcome());
    }
}

/// <summary>Handler de exemplo, com valor de retorno, que conta as chamadas.</summary>
public sealed class RecordingHandlerWithId(Guid id) : ICommandHandler<RegisterFanWithId, Guid>
{
    public int Calls { get; private set; }

    public Task<Result<Guid>> Handle(RegisterFanWithId command, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Result.Success(id));
    }
}

/// <summary>Handler de exemplo, com valor de retorno configurável (sucesso ou falha), que conta as chamadas.</summary>
public sealed class ConfigurableHandlerWithId(Func<Result<Guid>> outcome) : ICommandHandler<RegisterFanWithId, Guid>
{
    public int Calls { get; private set; }

    public Task<Result<Guid>> Handle(RegisterFanWithId command, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(outcome());
    }
}

/// <summary>Consulta de exemplo com um dado pessoal (e-mail) no payload.</summary>
public sealed record FindFanByEmail(string Email) : IQuery<string>;

/// <summary>Handler de consulta de exemplo que conta as chamadas e devolve o resultado configurado.</summary>
public sealed class RecordingQueryHandler(Func<Result<string>> outcome) : IQueryHandler<FindFanByEmail, string>
{
    public int Calls { get; private set; }

    public Task<Result<string>> Handle(FindFanByEmail query, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(outcome());
    }
}
