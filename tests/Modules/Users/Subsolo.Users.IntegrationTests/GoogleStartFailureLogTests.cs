using System.Globalization;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0021 IT-06 (§7.6, herança da revisão da SPEC-0020) — com a authority do Google apontando para um endereço sem
/// servidor, o início do login volta para <c>/entrar?erro=google</c> e o log da falha leva o motivo
/// <c>provider_error</c> e a exceção (para o diagnóstico), sem dados pessoais.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class GoogleStartFailureLogTests(PostgresFixture postgres, MailpitFixture mailpit)
{
    private const string RejectedMessage = "Entrada com Google recusada";

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-06")]
    [Trait("Kind", "Integration")]
    public async Task TheStartFailureIsLoggedWithTheReasonAndTheException()
    {
        var ct = TestContext.Current.CancellationToken;
        var deadAuthority = string.Create(
            CultureInfo.InvariantCulture, $"http://127.0.0.1:{MagicLinkHost.ClosedPort()}/{FakeGoogleProvider.Issuers.Verified}");
        var (api, _) = await GoogleHost.StartAsync(postgres, mailpit, GoogleHost.GoogleSettings(deadAuthority), ct);
        await using var __ = api;
        using var client = api.CreateUsersClient();

        using var start = await client.StartGoogleAsync("/curitiba", ct);

        start.RedirectTarget().ShouldBe(GoogleHost.ProviderErrorRedirect, "provedor fora do ar");
        var rejected = api.Logs
            .Where(log => (log.FormattedMessage ?? log.Body ?? string.Empty).Contains(RejectedMessage, StringComparison.Ordinal))
            .ToList();
        var log = rejected.ShouldHaveSingleItem($"esperado um log \"{RejectedMessage}\" (logs: {rejected.Count})");
        (log.FormattedMessage ?? string.Empty).ShouldContain("provider_error", Case.Sensitive, "o motivo é provider_error");
        log.Exception.ShouldNotBeNull("o log da falha do início leva a exceção, para o diagnóstico");
        log.Exception.ToString().ShouldNotContain("@", Case.Sensitive, "a exceção logada não leva e-mail");
    }
}
