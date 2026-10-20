using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Application.Features.FollowProfile;
using Subsolo.Modules.Engagement.Application.Features.Unfollow;
using Subsolo.Modules.Engagement.Domain.Follows;

namespace Subsolo.Modules.Engagement.Presentation.Features;

/// <summary>
/// SPEC-0027 — <c>PUT</c> (seguir) e <c>DELETE</c> (deixar de seguir) <c>/api/v1/me/follows/profiles/{profileId}</c>
/// (política <c>fan</c>, X-CSRF, rate limit <c>default</c>): 204, idempotentes; perfil inexistente ou não aprovado no PUT
/// → 404 <c>engagement.target_not_found</c>.
/// </summary>
internal sealed class FollowProfileEndpoints : IEndpoint
{
    public const string Route = "/api/v1/me/follows/profiles/{profileId:guid}";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut(Route, FollowAsync)
            .RequireEngagementFan(mutation: true)
            .WithName("FollowProfile")
            .WithSummary("Seguir perfil")
            .WithDescription(
                "O fã passa a seguir a banda, a casa ou o produtor (perfil aprovado; idempotente). Exige o cabeçalho X-CSRF.")
            .ProducesProblemCode(StatusCodes.Status404NotFound, EngagementErrors.TargetNotFound);

        app.MapDelete(Route, UnfollowAsync)
            .RequireEngagementFan(mutation: true)
            .WithName("UnfollowProfile")
            .WithSummary("Deixar de seguir perfil")
            .WithDescription("O fã deixa de seguir o perfil (idempotente: sem follow, nada muda). Exige o cabeçalho X-CSRF.");
    }

    private static async Task<IResult> FollowAsync(
        Guid profileId,
        ClaimsPrincipal user,
        [FromServices] ICommandHandler<FollowProfileCommand> handler,
        CancellationToken cancellationToken) =>
        EngagementEndpoints.UserId(user) is { } userId
            ? EngagementEndpoints.NoContentOrProblem(
                await handler.Handle(new FollowProfileCommand(userId, profileId), cancellationToken))
            : SecurityProblems.Unauthorized();

    private static async Task<IResult> UnfollowAsync(
        Guid profileId,
        ClaimsPrincipal user,
        [FromServices] ICommandHandler<UnfollowCommand> handler,
        CancellationToken cancellationToken) =>
        EngagementEndpoints.UserId(user) is { } userId
            ? EngagementEndpoints.NoContentOrProblem(
                await handler.Handle(new UnfollowCommand(userId, FollowTarget.Profile(profileId)), cancellationToken))
            : SecurityProblems.Unauthorized();
}
