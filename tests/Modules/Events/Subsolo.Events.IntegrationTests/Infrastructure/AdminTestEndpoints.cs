using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Users.Application.Features.SignIn;
using Subsolo.Modules.Users.Domain.Sessions;

namespace Subsolo.Events.IntegrationTests.Infrastructure;

/// <summary>
/// Dublê: endpoint que existe só neste projeto de teste (registrado pelo <see cref="AdminApiFactory"/>). Faz a entrada
/// pelo <see cref="ISignInService"/> do host — a sessão real, com os papéis de <c>Users:AdminEmails</c> — e devolve o
/// cookie de sessão na resposta.
/// </summary>
public sealed class AdminTestEndpoints : IEndpoint
{
    public const string SignIn = "/api/v1/__tests/events/sign-in";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(SignIn, async (SignInRequest request, ISignInService signIn, CancellationToken cancellationToken) =>
            {
                var result = await signIn.SignInAsync(request.Email, SignInMethod.MagicLink, cancellationToken);
                return result.IsSuccess ? Results.Ok() : ApiResults.Problem(result);
            })
            .AllowAnonymous();

    public sealed record SignInRequest(string Email);
}
