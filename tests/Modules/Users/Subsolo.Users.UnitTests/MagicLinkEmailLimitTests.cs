using Subsolo.Users.UnitTests.Infrastructure;

namespace Subsolo.Users.UnitTests;

/// <summary>
/// SPEC-0016 UT-02 — limite por e-mail: dados 3 pedidos para o mesmo e-mail em 15 min, o 4º responde sucesso (igual)
/// sem chamar o Notifications nem gravar link; 15 min e 1 s depois, o pedido manda o e-mail de novo. O limite é por
/// e-mail: outro e-mail no mesmo instante continua recebendo.
/// </summary>
public sealed class MagicLinkEmailLimitTests
{
    private const string Email = "limite@subsolo.test";

    [Fact]
    [Trait("Spec", "SPEC-0016:UT-02")]
    public async Task FourthRequestWithin15MinutesSucceedsWithoutSending()
    {
        var harness = new MagicLinkHarness();
        for (var request = 1; request <= 3; request++)
        {
            await harness.RequestTokenAsync(Email);
        }

        var fourth = await harness.RequestAsync(Email);

        fourth.IsSuccess.ShouldBeTrue("acima do limite por e-mail a resposta é igual (sucesso)");
        harness.Notifications.Requests.Count.ShouldBe(3, "o 4º pedido em 15 min não manda e-mail");
        harness.Tokens.All.Count.ShouldBe(3, "o 4º pedido em 15 min não grava link");
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:UT-02")]
    public async Task After15MinutesAndOneSecondTheEmailIsSentAgain()
    {
        var harness = new MagicLinkHarness();
        for (var request = 1; request <= 3; request++)
        {
            await harness.RequestTokenAsync(Email);
        }

        (await harness.RequestAsync(Email)).IsSuccess.ShouldBeTrue();
        harness.Notifications.Requests.Count.ShouldBe(3);

        harness.Clock.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));
        await harness.RequestTokenAsync(Email);

        harness.Notifications.Requests.Count.ShouldBe(4, "passada a janela de 15 min, o e-mail volta a sair");
    }

    [Fact]
    [Trait("Spec", "SPEC-0016:UT-02")]
    public async Task TheLimitIsPerEmail()
    {
        var harness = new MagicLinkHarness();
        for (var request = 1; request <= 3; request++)
        {
            await harness.RequestTokenAsync(Email);
        }

        await harness.RequestTokenAsync("outra.pessoa@subsolo.test");

        harness.Notifications.Requests.Count.ShouldBe(4, "outro e-mail não é afetado pelo limite do primeiro");
    }
}
