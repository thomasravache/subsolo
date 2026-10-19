using System.Data.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.GetShowDetail;

namespace Subsolo.Modules.Events.Presentation.Features.GetShowDetail;

/// <summary>
/// SPEC-0024 — <c>GET /api/v1/cities/{city}/shows/{slug}</c>, público (<c>AllowAnonymous</c>, rate limit
/// <c>default</c>): o show da página <c>/{cidade}/{show}</c>. Banco fora do ar → 503 em ProblemDetails, sem detalhe da
/// exceção (a página do web responde 503, nunca 200 vazio).
/// </summary>
internal sealed class GetShowDetailEndpoint : IEndpoint
{
    public const string Route = "/api/v1/cities/{city}/shows/{slug}";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Default)
            .WithName("GetShowDetail")
            .WithTags("Agenda")
            .WithSummary("Página do show")
            .WithDescription(
                "O show público da cidade pelo slug: cidade (com o fuso), início em UTC, casa e line-up (com o slug do " +
                "perfil quando houver), estilos, preço, link do ingresso, situação (cancelado e adiado continuam aqui) e " +
                "as variantes do flyer com as medidas da full.")
            .Produces<ShowDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status404NotFound, EventsErrors.ShowNotFound)
            .ProducesProblemCode(StatusCodes.Status404NotFound, EventsErrors.CityNotFound)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited)
            .ProducesProblemCode(StatusCodes.Status503ServiceUnavailable, SecurityProblems.ServiceUnavailable);

    private static async Task<IResult> HandleAsync(
        string city,
        string slug,
        [FromServices] IQueryHandler<GetShowDetailQuery, ShowDetailResponse> handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await handler.Handle(new GetShowDetailQuery(city, slug), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
        }
        catch (Exception exception) when (IsStoreFailure(exception) && !cancellationToken.IsCancellationRequested)
        {
            return SecurityProblems.Unavailable();
        }
    }

    /// <summary>Falha de acesso ao banco (o Npgsql e o EF Core embrulham <see cref="DbException"/> e timeouts).</summary>
    private static bool IsStoreFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }
}
