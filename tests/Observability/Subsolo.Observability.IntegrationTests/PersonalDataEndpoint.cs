using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Subsolo.Common.Presentation;

namespace Subsolo.Observability.IntegrationTests;

/// <summary>
/// Dublê de teste (SPEC-0009 IT-02): endpoint que loga o e-mail recebido e o cookie da requisição e os coloca como
/// atributos do span atual — o descuido que o redator do ServiceDefaults precisa neutralizar. Anônimo porque o
/// fallback de autorização da SPEC-0010 exige usuário autenticado e o IT-02 precisa chegar ao handler (204), não ao 401.
/// </summary>
public sealed partial class PersonalDataEndpoint : IEndpoint
{
    public const string Route = "/__tests/observability/personal-data";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, IResult (string email, HttpRequest request, ILogger<PersonalDataEndpoint> logger) =>
        {
            var cookie = request.Headers.Cookie.ToString();
            LogSignup(logger, email, cookie);
            Activity.Current?.SetTag("user.email", email);
            Activity.Current?.SetTag("http.request.header.cookie", cookie);
            return Results.NoContent();
        }).AllowAnonymous();

    [LoggerMessage(Level = LogLevel.Information, Message = "Cadastro de {Email} com o cookie {Cookie}")]
    private static partial void LogSignup(ILogger logger, string email, string cookie);
}
