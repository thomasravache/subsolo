using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Users.Application.Features.SignIn;
using Subsolo.Modules.Users.Domain.Sessions;

namespace Subsolo.Notifications.IntegrationTests.Infrastructure;

/// <summary>
/// Dublê: endpoint que existe só neste projeto de teste (registrado pelo <see cref="NotificationsApiFactory"/>; copiado
/// do <c>EngagementTestEndpoints</c>). Faz a entrada pelo <see cref="ISignInService"/> do host — a sessão real — e
/// devolve <c>{userId, isNewUser}</c>, com o cookie de sessão na resposta.
/// </summary>
public sealed class NotificationsTestEndpoints : IEndpoint
{
    public const string SignIn = "/api/v1/__tests/notifications/sign-in";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(SignIn, async (SignInRequest request, ISignInService signIn, CancellationToken cancellationToken) =>
            {
                var result = await signIn.SignInAsync(request.Email, SignInMethod.MagicLink, cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
            })
            .AllowAnonymous();

    public sealed record SignInRequest(string Email);
}
