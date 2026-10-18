using System.Security.Claims;
using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Application.Features.SignIn;
using Subsolo.Modules.Users.Application.Features.SignInWithGoogle;
using Subsolo.Modules.Users.Domain.Sessions;

namespace Subsolo.Users.UnitTests;

/// <summary>
/// SPEC-0020 UT-01 — o tratador do retorno do Google (<see cref="GoogleSignInCallback"/>) com um
/// <see cref="ClaimsPrincipal"/> de teste (claims com os nomes do OIDC): com <c>email_verified=true</c>, chama
/// <c>SignInAsync(email, Google)</c> e redireciona para o <c>returnTo</c> saneado (<c>/curitiba</c> mantido;
/// <c>//evil.com</c>, <c>https://evil.com</c> e nulo viram <c>/</c>); com <c>email_verified</c> falso ou ausente, ou sem
/// e-mail, não chama o serviço e redireciona para <c>/entrar?erro=google_email</c>.
/// </summary>
public sealed class GoogleSignInCallbackTests
{
    private const string Email = "fa@subsolo.test";

    private const string EmailNotVerified = "/entrar?erro=google_email";

    [Theory]
    [Trait("Spec", "SPEC-0020:UT-01")]
    [InlineData("/curitiba", "/curitiba")]
    [InlineData("/conta", "/conta")]
    [InlineData("//evil.com", "/")]
    [InlineData("https://evil.com", "/")]
    [InlineData("/\\evil.com", "/")]
    [InlineData(null, "/")]
    public async Task VerifiedEmailSignsInWithGoogleAndGoesBackToTheSanitizedReturnTo(string? returnTo, string expected)
    {
        var signIn = new RecordingSignInService();
        var callback = new GoogleSignInCallback(signIn);

        var outcome = await callback.CompleteAsync(
            Principal(("email", Email, ClaimValueTypes.String), ("email_verified", "true", ClaimValueTypes.Boolean)),
            returnTo,
            TestContext.Current.CancellationToken);

        signIn.Calls.ShouldHaveSingleItem().ShouldBe((Email, SignInMethod.Google));
        outcome.SignedIn.ShouldBeTrue();
        outcome.RedirectTo.ShouldBe(expected, $"returnTo \"{returnTo}\"");
    }

    [Fact]
    [Trait("Spec", "SPEC-0020:UT-01")]
    public async Task EmailVerifiedAsTextTrueAlsoSignsIn()
    {
        var signIn = new RecordingSignInService();
        var callback = new GoogleSignInCallback(signIn);

        var outcome = await callback.CompleteAsync(
            Principal(("email", Email, ClaimValueTypes.String), ("email_verified", "true", ClaimValueTypes.String)),
            "/curitiba",
            TestContext.Current.CancellationToken);

        signIn.Calls.ShouldHaveSingleItem().ShouldBe((Email, SignInMethod.Google));
        outcome.RedirectTo.ShouldBe("/curitiba");
    }

    [Theory]
    [Trait("Spec", "SPEC-0020:UT-01")]
    [InlineData("email_verified=false")]
    [InlineData("email_verified ausente")]
    [InlineData("email_verified vazio")]
    [InlineData("sem e-mail")]
    [InlineData("e-mail vazio")]
    [InlineData("sem claims")]
    public async Task WithoutAVerifiedEmailItDoesNotSignInAndGoesToTheEmailError(string scenario)
    {
        var signIn = new RecordingSignInService();
        var callback = new GoogleSignInCallback(signIn);
        var principal = scenario switch
        {
            "email_verified=false" => Principal(("email", Email, ClaimValueTypes.String), ("email_verified", "false", ClaimValueTypes.Boolean)),
            "email_verified ausente" => Principal(("email", Email, ClaimValueTypes.String)),
            "email_verified vazio" => Principal(("email", Email, ClaimValueTypes.String), ("email_verified", string.Empty, ClaimValueTypes.String)),
            "sem e-mail" => Principal(("email_verified", "true", ClaimValueTypes.Boolean)),
            "e-mail vazio" => Principal(("email", string.Empty, ClaimValueTypes.String), ("email_verified", "true", ClaimValueTypes.Boolean)),
            _ => Principal(),
        };

        var outcome = await callback.CompleteAsync(principal, "/curitiba", TestContext.Current.CancellationToken);

        signIn.Calls.ShouldBeEmpty($"{scenario}: o ISignInService não pode ser chamado");
        outcome.SignedIn.ShouldBeFalse(scenario);
        outcome.RedirectTo.ShouldBe(EmailNotVerified, scenario);
    }

    private static ClaimsPrincipal Principal(params (string Type, string Value, string ValueType)[] claims) =>
        new(new ClaimsIdentity(
            claims.Select(claim => new Claim(claim.Type, claim.Value, claim.ValueType, "https://accounts.google.test")),
            "google"));

    /// <summary>Dublê do <see cref="ISignInService"/>: guarda cada chamada e responde sucesso.</summary>
    private sealed class RecordingSignInService : ISignInService
    {
        public List<(string Email, SignInMethod Method)> Calls { get; } = [];

        public Task<Result<SignInResult>> SignInAsync(string email, SignInMethod method, CancellationToken cancellationToken)
        {
            Calls.Add((email, method));
            return Task.FromResult(Result.Success(new SignInResult(Guid.NewGuid(), IsNewUser: true)));
        }
    }
}
