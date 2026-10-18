using Subsolo.Modules.Users.Application;

namespace Subsolo.Users.UnitTests;

/// <summary>
/// SPEC-0020 UT-02 — <c>Users:Google</c> sem Authority ou sem ClientId deixa o provedor desligado
/// (<c>providers.google=false</c>); com os dois, ligado (o segredo não decide). Sem a seção, o padrão é desligado.
/// </summary>
public sealed class GoogleOptionsTests
{
    private const string Authority = "http://localhost:8090/default";

    private const string ClientId = "subsolo-local";

    [Fact]
    [Trait("Spec", "SPEC-0020:UT-02")]
    public void WithoutTheSectionGoogleIsOff() =>
        new UsersOptions().Google.IsEnabled.ShouldBeFalse("sem Users:Google o botão não aparece");

    [Theory]
    [Trait("Spec", "SPEC-0020:UT-02")]
    [InlineData(null, ClientId)]
    [InlineData("", ClientId)]
    [InlineData("   ", ClientId)]
    [InlineData(Authority, null)]
    [InlineData(Authority, "")]
    [InlineData(Authority, "   ")]
    [InlineData(null, null)]
    public void WithoutAuthorityOrClientIdGoogleIsOff(string? authority, string? clientId) =>
        new GoogleOptions { Authority = authority, ClientId = clientId, ClientSecret = "segredo" }
            .IsEnabled.ShouldBeFalse($"Authority \"{authority}\", ClientId \"{clientId}\"");

    [Theory]
    [Trait("Spec", "SPEC-0020:UT-02")]
    [InlineData("segredo")]
    [InlineData(null)]
    public void WithAuthorityAndClientIdGoogleIsOn(string? clientSecret) =>
        new GoogleOptions { Authority = Authority, ClientId = ClientId, ClientSecret = clientSecret }
            .IsEnabled.ShouldBeTrue("Authority e ClientId configurados ligam o Google");
}
