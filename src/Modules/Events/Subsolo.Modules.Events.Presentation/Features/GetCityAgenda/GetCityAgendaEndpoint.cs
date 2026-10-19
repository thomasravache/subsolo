using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;

namespace Subsolo.Modules.Events.Presentation.Features.GetCityAgenda;

/// <summary>
/// SPEC-0008 — <c>GET /api/v1/cities/{slug}/agenda?from=&amp;to=</c>, público (<c>AllowAnonymous</c>, rate limit
/// <c>default</c> — SPEC-0014). SPEC-0025 — filtro <c>styles=death,grind</c>, <c>period</c> e <c>total</c> na resposta;
/// <c>from</c>/<c>to</c> mal formados → 400 <c>agenda.invalid_range</c> (ver <see cref="AgendaDateBinding"/>).
/// </summary>
internal sealed class GetCityAgendaEndpoint : IEndpoint
{
    public const string Route = "/api/v1/cities/{slug}/agenda";

    /// <summary>Nome do endpoint (também identifica a agenda nas falhas de leitura das datas).</summary>
    public const string Name = "GetCityAgenda";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Default)
            .AddEndpointFilter(AgendaDateBinding.RejectMalformedDatesAsync)
            .WithName(Name)
            .WithTags("Agenda")
            .WithSummary("Agenda da cidade")
            .WithDescription(
                "Cidade e shows entre from e to (datas locais da cidade, aaaa-mm-dd; no máximo 180 dias; sem from e to, " +
                "de 6 h atrás até 90 dias, com period de hoje a hoje + 90) e, com styles (slugs separados por vírgula), " +
                "só os shows com algum desses estilos (estilos inexistentes são ignorados). Traz o period e o total.")
            .Produces<CityAgendaResponse>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status400BadRequest, AgendaErrors.InvalidSlug)
            .WithMetadata(
                new ProblemCodeMetadata(StatusCodes.Status400BadRequest, AgendaErrors.InvalidRange),
                new ProblemCodeMetadata(StatusCodes.Status400BadRequest, AgendaErrors.InvalidStyles))
            .ProducesProblemCode(StatusCodes.Status404NotFound, AgendaErrors.CityNotFound);

    private static async Task<IResult> HandleAsync(
        // Só documenta o formato no OpenAPI; quem valida (com o código agenda.invalid_slug) é o validador.
        [RegularExpression(GetCityAgendaQuery.SlugPattern)] string slug,
        DateOnly? from,
        DateOnly? to,
        [Description("Slugs de estilo separados por vírgula (no máximo 20), ex.: death,grind.")] string? styles,
        IValidator<GetCityAgendaQuery> validator,
        IQueryHandler<GetCityAgendaQuery, CityAgendaResponse> handler,
        CancellationToken cancellationToken)
    {
        var query = new GetCityAgendaQuery(slug, from, to, GetCityAgendaQuery.ParseStyles(styles));

        // O decorator comum valida só comandos: a consulta é validada aqui, com o código específico da falha.
        if (await EndpointValidation.ValidateAsync(validator, query, "Consulta da agenda inválida.", cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var result = await handler.Handle(query, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}

/// <summary>
/// SPEC-0025 — <c>from</c>/<c>to</c> que não são datas (ex.: <c>2026-13-01</c>, <c>12/11/2026</c>) viram 400
/// <c>agenda.invalid_range</c> em ProblemDetails, em vez da falha genérica de leitura do Minimal API. A leitura falha antes
/// do handler: em Development (<c>ThrowOnBadRequest</c>) ela lança <see cref="BadHttpRequestException"/>, tratada por
/// <see cref="AgendaDateBindingExceptionHandler"/>; nos demais ambientes ela marca a resposta com 400 e segue pelos
/// filtros, onde <see cref="RejectMalformedDatesAsync"/> responde.
/// </summary>
internal static class AgendaDateBinding
{
    /// <summary>O formato das datas da agenda.</summary>
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>O ProblemDetails de data mal formada, com os campos (<c>From</c>, <c>To</c>) que não são datas.</summary>
    public static IResult Problem(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string[] fields =
        [
            .. new[] { (Query: "from", Field: nameof(GetCityAgendaQuery.From)), (Query: "to", Field: nameof(GetCityAgendaQuery.To)) }
                .Where(item => IsMalformed(request.Query[item.Query]))
                .Select(item => item.Field),
        ];
        return ApiResults.Problem(Result.Failure(AgendaErrors.MalformedDateError(fields)));
    }

    /// <summary>
    /// Valor presente que não é uma data <c>aaaa-mm-dd</c> estrita. SPEC-0026 (H25-03): a leitura do Minimal API (cultura
    /// invariante) aceitaria também <c>12/11/2026</c> (mês/dia); aqui só vale o formato do contrato.
    /// </summary>
    private static bool IsMalformed(StringValues values) =>
        !StringValues.IsNullOrEmpty(values)
        && !DateOnly.TryParseExact(values.ToString(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    /// <summary>
    /// Filtro do endpoint: com a leitura dos parâmetros já reprovada (status 400) ou com uma data fora do formato estrito
    /// (lida pelo Minimal API em outro formato), responde o código da agenda.
    /// </summary>
    public static async ValueTask<object?> RejectMalformedDatesAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var request = context.HttpContext.Request;
        return context.HttpContext.Response.StatusCode == StatusCodes.Status400BadRequest
               || IsMalformed(request.Query["from"])
               || IsMalformed(request.Query["to"])
            ? Problem(request)
            : await next(context);
    }
}
