using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Application.Features.FollowCity;
using Subsolo.Modules.Engagement.Application.Features.Unfollow;
using Subsolo.Modules.Engagement.Domain.Follows;

namespace Subsolo.Modules.Engagement.Presentation.Features;

/// <summary>
/// SPEC-0027 — <c>PUT</c> (seguir) e <c>DELETE</c> (deixar de seguir) <c>/api/v1/me/follows/cities/{slug}</c>
/// (política <c>fan</c>, X-CSRF, rate limit <c>default</c>): 204, idempotentes; cidade inexistente no PUT → 404
/// <c>engagement.target_not_found</c>.
/// </summary>
internal sealed class FollowCityEndpoints : IEndpoint
{
    public const string Route = "/api/v1/me/follows/cities/{slug}";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut(Route, FollowAsync)
            .RequireEngagementFan(mutation: true)
            .WithName("FollowCity")
            .WithSummary("Seguir cidade")
            .WithDescription("O fã passa a seguir a cidade (idempotente: seguir de novo não muda nada). Exige o cabeçalho X-CSRF.")
            .ProducesProblemCode(StatusCodes.Status404NotFound, EngagementErrors.TargetNotFound);

        app.MapDelete(Route, UnfollowAsync)
            .RequireEngagementFan(mutation: true)
            .WithName("UnfollowCity")
            .WithSummary("Deixar de seguir cidade")
            .WithDescription("O fã deixa de seguir a cidade (idempotente: sem follow, nada muda). Exige o cabeçalho X-CSRF.");
    }

    private static async Task<IResult> FollowAsync(
        string slug,
        ClaimsPrincipal user,
        [FromServices] ICommandHandler<FollowCityCommand> handler,
        CancellationToken cancellationToken) =>
        EngagementEndpoints.UserId(user) is { } userId
            ? EngagementEndpoints.NoContentOrProblem(await handler.Handle(new FollowCityCommand(userId, slug), cancellationToken))
            : SecurityProblems.Unauthorized();

    private static async Task<IResult> UnfollowAsync(
        string slug,
        ClaimsPrincipal user,
        [FromServices] ICommandHandler<UnfollowCommand> handler,
        CancellationToken cancellationToken) =>
        EngagementEndpoints.UserId(user) is { } userId
            ? EngagementEndpoints.NoContentOrProblem(
                await handler.Handle(new UnfollowCommand(userId, FollowTarget.City(slug)), cancellationToken))
            : SecurityProblems.Unauthorized();
}
