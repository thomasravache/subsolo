using Subsolo.Common.Domain;

namespace Subsolo.Modules.ResultSample.IntegrationEvents;

/// <summary>
/// SPEC-0018 IT-05 (Emenda 1, ADR-0001 esclarecido) — dublê: contrato público que usa só os tipos de resultado de
/// Common.Domain (<c>Result</c>, <c>Result&lt;T&gt;</c>, <c>Error</c>, <c>ErrorType</c>); é permitido.
/// </summary>
public interface IResultSampleApi
{
    Task<Result> SendAsync(string recipient, CancellationToken cancellationToken);

    Task<Result<int>> CountAsync(CancellationToken cancellationToken);

    Error LastError { get; }

    ErrorType LastErrorType { get; }
}
