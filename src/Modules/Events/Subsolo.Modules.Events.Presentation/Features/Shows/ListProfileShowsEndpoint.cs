using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.ListProfileShows;

namespace Subsolo.Modules.Events.Presentation.Features.Shows;

/// <summary>
/// SPEC-0026 — <c>GET /api/v1/shows?profileId={id}&amp;period=upcoming|past</c>, público (<c>AllowAnonymous</c>, rate
/// limit <c>default</c>): os shows da página do perfil. <c>profileId</c> ausente ou fora do formato uuid e <c>period</c>
/// inválido → 400 <c>events.invalid_query</c> (a falha de leitura do uuid passa por <see cref="RejectMalformedIdAsync"/>
/// e, em Development, pelo <c>AgendaDateBindingExceptionHandler</c>).
/// </summary>
internal sealed class ListProfileShowsEndpoint : IEndpoint
{
    /// <summary>Nome do endpoint (também identifica a consulta nas falhas de leitura do <c>profileId</c>).</summary>
    public const string Name = "ListProfileShows";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(ShowsEndpoints.ShowsRoute, HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Default)
            .AddEndpointFilter(RejectMalformedIdAsync)
            .WithName(Name)
            .WithTags("Agenda")
            .WithSummary("Shows do perfil")
            .WithDescription(
                "Os shows em que o perfil é organizador, casa ou banda do line-up, com o resumo da agenda e a cidade do " +
                "show: os próximos (period=upcoming, até 50, por data) ou os passados (period=past, até 20, mais " +
                "recentes primeiro).")
            .Produces<ProfileShowsBody>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, EventsErrors.InvalidQuery)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited);

    private static async Task<IResult> HandleAsync(
        [FromQuery] Guid? profileId,
        [FromQuery] string? period,
        [FromServices] IQueryHandler<ListProfileShowsQuery, ProfileShowsResponse> handler,
        CancellationToken cancellationToken)
    {
        if (profileId is not { } id)
        {
            return MissingProfileId();
        }

        var result = await handler.Handle(new ListProfileShowsQuery(id, period), cancellationToken);
        return result.IsSuccess
            ? Results.Ok(new ProfileShowsBody([.. result.Value.Shows.Cast<ProfileShowResponse>()]))
            : ApiResults.Problem(result);
    }

    /// <summary>O 400 de <c>profileId</c> ausente ou mal formado.</summary>
    public static IResult MissingProfileId() =>
        ApiResults.Problem(Result.Failure(EventsErrors.InvalidQueryError("profileId", "Informe o id (uuid) do perfil.")));

    /// <summary>Filtro do endpoint: com a leitura dos parâmetros já reprovada (status 400), responde o código da consulta.</summary>
    private static async ValueTask<object?> RejectMalformedIdAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        context.HttpContext.Response.StatusCode == StatusCodes.Status400BadRequest
            ? MissingProfileId()
            : await next(context);
}

/// <summary>SPEC-0026 — corpo de <c>GET /api/v1/shows?profileId=&amp;period=</c>: <c>{ shows }</c>.</summary>
internal sealed record ProfileShowsBody(IReadOnlyList<ProfileShowResponse> Shows);
