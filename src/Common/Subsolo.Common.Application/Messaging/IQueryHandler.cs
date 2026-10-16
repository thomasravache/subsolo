using Subsolo.Common.Domain;

namespace Subsolo.Common.Application.Messaging;

/// <summary>SPEC-0007 — handler de uma <see cref="IQuery{TResponse}"/>.</summary>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}
