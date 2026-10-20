using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Application.Features.AttendShow;
using Subsolo.Modules.Engagement.Application.Features.UnattendShow;

namespace Subsolo.Modules.Engagement.Presentation.Features;

/// <summary>
/// SPEC-0029 — <c>PUT</c> (marcar "vou") e <c>DELETE</c> (desmarcar) <c>/api/v1/me/attendances/{showId}</c> (política
/// <c>fan</c>, X-CSRF, rate limit <c>default</c>): 204, idempotentes; no PUT, show inexistente → 404
/// <c>engagement.show_not_found</c> e cancelado, adiado ou já começado → 409 <c>engagement.show_not_attendable</c>.
/// </summary>
internal sealed class AttendShowEndpoints : IEndpoint
{
    public const string Route = "/api/v1/me/attendances/{showId:guid}";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut(Route, AttendAsync)
            .RequireEngagementFan(mutation: true, EngagementEndpoints.AttendanceTag)
            .WithName("AttendShow")
            .WithSummary("Marcar vou")
            .WithDescription(
                "O fã marca \"vou\" no show agendado que ainda não começou (idempotente: marcar de novo não muda nada). " +
                "Exige o cabeçalho X-CSRF.")
            .ProducesProblemCode(StatusCodes.Status404NotFound, EngagementErrors.ShowNotFound)
            .ProducesProblemCode(StatusCodes.Status409Conflict, EngagementErrors.ShowNotAttendable);

        app.MapDelete(Route, UnattendAsync)
            .RequireEngagementFan(mutation: true, EngagementEndpoints.AttendanceTag)
            .WithName("UnattendShow")
            .WithSummary("Desmarcar vou")
            .WithDescription("O fã desmarca o \"vou\" no show (idempotente: sem \"vou\", nada muda). Exige o cabeçalho X-CSRF.");
    }

    private static async Task<IResult> AttendAsync(
        Guid showId,
        ClaimsPrincipal user,
        [FromServices] ICommandHandler<AttendShowCommand> handler,
        CancellationToken cancellationToken) =>
        EngagementEndpoints.UserId(user) is { } userId
            ? EngagementEndpoints.NoContentOrProblem(await handler.Handle(new AttendShowCommand(userId, showId), cancellationToken))
            : SecurityProblems.Unauthorized();

    private static async Task<IResult> UnattendAsync(
        Guid showId,
        ClaimsPrincipal user,
        [FromServices] ICommandHandler<UnattendShowCommand> handler,
        CancellationToken cancellationToken) =>
        EngagementEndpoints.UserId(user) is { } userId
            ? EngagementEndpoints.NoContentOrProblem(await handler.Handle(new UnattendShowCommand(userId, showId), cancellationToken))
            : SecurityProblems.Unauthorized();
}
