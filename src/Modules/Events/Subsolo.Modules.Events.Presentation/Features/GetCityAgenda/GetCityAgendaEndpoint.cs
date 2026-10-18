using System.ComponentModel.DataAnnotations;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;

namespace Subsolo.Modules.Events.Presentation.Features.GetCityAgenda;

/// <summary>SPEC-0008 — <c>GET /api/v1/cities/{slug}/agenda?from=&amp;to=</c>, público (<c>AllowAnonymous</c>, rate limit <c>default</c> — SPEC-0014).</summary>
internal sealed class GetCityAgendaEndpoint : IEndpoint
{
    public const string Route = "/api/v1/cities/{slug}/agenda";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Default)
            .WithName("GetCityAgenda")
            .WithTags("Agenda")
            .WithSummary("Agenda da cidade")
            .WithDescription(
                "Cidade e shows entre from e to (padrão: de hoje, no fuso da cidade, até +60 dias; máximo 180 dias).")
            .Produces<CityAgendaResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<IResult> HandleAsync(
        // Só documenta o formato no OpenAPI; quem valida (com o código agenda.invalid_slug) é o validador.
        [RegularExpression(GetCityAgendaQuery.SlugPattern)] string slug,
        DateOnly? from,
        DateOnly? to,
        IValidator<GetCityAgendaQuery> validator,
        IQueryHandler<GetCityAgendaQuery, CityAgendaResponse> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetCityAgendaQuery(slug, from, to);

        // O decorator comum valida só comandos: a consulta é validada aqui, com o código específico da falha.
        if (await EndpointValidation.ValidateAsync(validator, query, "Consulta da agenda inválida.", cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var result = await handler.Handle(query, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
