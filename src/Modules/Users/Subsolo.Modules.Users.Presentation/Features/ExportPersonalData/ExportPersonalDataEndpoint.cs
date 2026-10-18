using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Users.Application.Features.ExportPersonalData;

namespace Subsolo.Modules.Users.Presentation.Features.ExportPersonalData;

/// <summary>
/// SPEC-0019 — <c>GET /api/v1/me/export</c> (política <c>fan</c>, rate limit <c>default</c>, <c>Cache-Control:
/// no-store</c>): baixa, como anexo <c>subsolo-meus-dados.json</c>, tudo o que o Subsolo guarda do usuário (LGPD art. 18).
/// </summary>
internal sealed class ExportPersonalDataEndpoint : IEndpoint
{
    public const string Route = "/api/v1/me/export";

    public const string FileName = "subsolo-meus-dados.json";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(Route, HandleAsync)
            .RequireAuthorization(AuthorizationPolicies.Fan)
            .RequireRateLimiting(RateLimitPolicies.Default)
            .RequireNoStore()
            .WithName("ExportPersonalData")
            .WithTags("Conta")
            .WithSummary("Baixar meus dados")
            .WithDescription(
                "Arquivo JSON com a conta, as sessões (sem token nem hash) e os dados de cada módulo sobre o usuário.")
            .Produces<PersonalDataExport>(StatusCodes.Status200OK)
            .ProducesProblemCode(StatusCodes.Status401Unauthorized, SecurityProblems.AuthRequired)
            .ProducesProblemCode(StatusCodes.Status429TooManyRequests, SecurityProblems.RateLimited)
            .ProducesProblemCode(StatusCodes.Status503ServiceUnavailable, SecurityProblems.ServiceUnavailable);

    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        [FromServices] IQueryHandler<ExportPersonalDataQuery, PersonalDataExport> handler,
        CancellationToken cancellationToken)
    {
        if (AccountUser.Id(httpContext.User) is not { } userId)
        {
            return SecurityProblems.Unauthorized();
        }

        var result = await handler.Handle(new ExportPersonalDataQuery(userId), cancellationToken);
        if (result.IsFailure)
        {
            return ApiResults.Problem(result);
        }

        var disposition = new ContentDispositionHeaderValue("attachment");
        disposition.SetHttpFileName(FileName);
        httpContext.Response.Headers.ContentDisposition = disposition.ToString();
        return Results.Ok(result.Value);
    }
}
