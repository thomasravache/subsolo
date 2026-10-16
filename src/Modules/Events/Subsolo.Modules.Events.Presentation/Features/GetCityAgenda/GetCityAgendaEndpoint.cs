using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;

namespace Subsolo.Modules.Events.Presentation.Features.GetCityAgenda;

/// <summary>SPEC-0008 — <c>GET /api/v1/cities/{slug}/agenda?from=&amp;to=</c>, público (<c>AllowAnonymous</c>).</summary>
internal sealed class GetCityAgendaEndpoint : IEndpoint
{
    public const string Route = "/api/v1/cities/{slug}/agenda";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .AllowAnonymous()
            .WithName("GetCityAgenda")
            .WithTags("Agenda")
            .WithSummary("Agenda da cidade")
            .WithDescription(
                "Cidade e shows entre from e to (padrão: de hoje, no fuso da cidade, até +60 dias; máximo 180 dias).")
            .Produces<CityAgendaResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<IResult> HandleAsync(
        string slug,
        DateOnly? from,
        DateOnly? to,
        IValidator<GetCityAgendaQuery> validator,
        IQueryHandler<GetCityAgendaQuery, CityAgendaResponse> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetCityAgendaQuery(slug, from, to);

        // O decorator comum valida só comandos: a consulta é validada aqui, com o código específico da falha.
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ApiResults.Problem(Result.Failure(ValidationError(validation)));
        }

        var result = await handler.Handle(query, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }

    /// <summary>Erro de validação com o código da primeira falha (slug antes do período) e os campos inválidos.</summary>
    private static Error ValidationError(FluentValidation.Results.ValidationResult validation)
    {
        var fields = validation.Errors
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        return Error.Validation(validation.Errors[0].ErrorCode, "Consulta da agenda inválida.", fields);
    }
}
