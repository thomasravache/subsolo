using Subsolo.Common.Domain;

namespace Subsolo.Common.Application.Messaging;

/// <summary>SPEC-0007 — handler de um <see cref="ICommand"/>.</summary>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<Result> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>SPEC-0007 — handler de um <see cref="ICommand{TResponse}"/>.</summary>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}
