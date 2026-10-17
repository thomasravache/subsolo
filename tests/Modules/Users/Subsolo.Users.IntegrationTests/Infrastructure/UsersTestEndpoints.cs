using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Users.Application.Features.SignIn;
using Subsolo.Modules.Users.Domain.Sessions;

namespace Subsolo.Users.IntegrationTests.Infrastructure;

/// <summary>
/// Dublês: endpoints que existem só neste projeto de teste (registrados pelo <see cref="UsersApiFactory"/>). O
/// <see cref="SignIn"/> faz a entrada pelo <see cref="ISignInService"/> do host (os métodos de login são das próximas
/// specs) e devolve <c>{userId, isNewUser}</c>, com o cookie de sessão na resposta; o <see cref="Admin"/> exige a
/// política <c>admin</c> (IT-08).
/// </summary>
public sealed class UsersTestEndpoints : IEndpoint
{
    public const string SignIn = "/api/v1/__tests/users/sign-in";

    public const string Admin = "/api/v1/__tests/users/admin";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(SignIn, async (SignInRequest request, ISignInService signIn, CancellationToken cancellationToken) =>
            {
                var result = await signIn.SignInAsync(request.Email, SignInMethod.MagicLink, cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : ApiResults.Problem(result);
            })
            .AllowAnonymous();
        app.MapGet(Admin, () => Results.Ok()).RequireAuthorization("admin");
    }

    public sealed record SignInRequest(string Email);
}
