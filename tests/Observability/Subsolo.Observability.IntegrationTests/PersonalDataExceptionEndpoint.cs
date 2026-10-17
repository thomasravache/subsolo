using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Presentation;

namespace Subsolo.Observability.IntegrationTests;

/// <summary>
/// Dublê de teste (SPEC-0009 IT-02, contrato v2): endpoint que lança uma exceção com o e-mail recebido na mensagem,
/// registrando-a antes no span atual (<see cref="Activity.AddException"/>, como o <c>LoggingDecorator</c> faz) — o
/// <c>UseExceptionHandler</c> da API loga a exceção e responde 500.
/// </summary>
public sealed class PersonalDataExceptionEndpoint : IEndpoint
{
    public const string Route = "/__tests/observability/personal-data-exception";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, IResult (string email) =>
        {
            var exception = new InvalidOperationException($"não foi possível avisar {email}");
            Activity.Current?.AddException(exception);
            throw exception;
        });
}
