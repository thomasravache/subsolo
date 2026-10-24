using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Moderation.Application.Features.ListReports;

namespace Subsolo.Modules.Moderation.Presentation.Features;

/// <summary>
/// SPEC-0033 — <c>GET /api/v1/admin/reports</c> (política <c>admin</c>, <c>no-store</c>): a fila de denúncias, um item
/// por show, mais antigos primeiro, sem quem denunciou. Sem pendentes, <c>[]</c>.
/// </summary>
internal sealed class ListReportsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet(ModerationEndpoints.AdminReportsRoute, HandleAsync)
            .RequireModerationAdmin(mutation: false)
            .WithName("ListReports")
            .WithSummary("Fila de denúncias")
            .WithDescription(
                "Shows com denúncias pendentes, mais antigos primeiro (pela primeira denúncia): título, data, casa, a " +
                "quantidade por motivo e até três detalhes mais recentes. Não identifica quem denunciou.")
            .Produces<IReadOnlyList<ReportQueueItem>>(StatusCodes.Status200OK);

    private static async Task<IResult> HandleAsync(
        [FromServices] IQueryHandler<ListReportsQuery, IReadOnlyList<ReportQueueItem>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListReportsQuery(), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
    }
}
