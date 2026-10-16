using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Presentation;

namespace Subsolo.Api.IntegrationTests;

/// <summary>
/// Dublê de teste: endpoint de exemplo que lança uma exceção não tratada. Anônimo porque o fallback de autorização
/// da SPEC-0010 exige usuário autenticado e o IT-05 da SPEC-0007 precisa chegar à exceção (500), não ao 401.
/// </summary>
public sealed class ThrowingEndpoint : IEndpoint
{
    public const string Route = "/__tests/throws";

    /// <summary>Texto que não pode vazar na resposta.</summary>
    public const string SecretDetail = "detalhe-interno-que-nao-pode-vazar";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, IResult () => throw new InvalidOperationException(SecretDetail)).AllowAnonymous();
}
