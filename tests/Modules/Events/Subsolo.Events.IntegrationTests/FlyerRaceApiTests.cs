using System.Net;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0024 — corridas com a limpeza dos flyers (revisão da SPEC-0023), no Subsolo.Api em memória com o PostgreSQL
/// real; a limpeza no meio é simulada pelo <see cref="CleanupRace"/> (linha apagada no banco e arquivos apagados do
/// <c>Media:Root</c> entre a leitura e a gravação):
/// <list type="bullet">
/// <item>H23-01 — o envio de um flyer cuja linha existe mas cujos arquivos sumiram regrava as variantes;</item>
/// <item>H23-02 — a limpeza apagou o flyer entre o <c>ExistsAsync</c> e o <c>SaveChanges</c> do cadastro ou da edição:
/// 400 <c>events.flyer_not_found</c> (nunca 500) e nada gravado; o reenvio do mesmo arquivo com a linha apagada depois da
/// leitura (o <c>Renew</c>) reprocessa e grava de novo, em vez de 500.</item>
/// </list>
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class FlyerRaceApiTests(PostgresFixture postgres)
{
    private async Task<(FlyersApiFactory Api, CleanupRace Race)> FactoryAsync(CancellationToken ct)
    {
        var connectionString = await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        FlyersApiFactory? api = null;
        var race = new CleanupRace(connectionString, () => api!.MediaRoot);
        api = new FlyersApiFactory(connectionString, configureServices: race.Install);
        return (api, race);
    }

    [Fact]
    [Trait("Spec", "SPEC-0024:H23-01")]
    [Trait("Kind", "Integration")]
    public async Task UploadingAgainAFlyerWhoseFilesAreMissingWritesTheVariantsAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, _) = await FactoryAsync(ct);
        await using var _ = api;
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var __ = admin;
        var image = TestImages.Jpeg(600, 900, seed: 71);
        FlyerUploaded first;
        using (var response = await admin.UploadAsync(image, ct))
        {
            first = await FlyerUploaded.ReadAsync(response, HttpStatusCode.Created, ct);
        }

        foreach (var (_, _, relativePath) in first.Variants)
        {
            File.Delete(Path.Combine(api.MediaRoot, relativePath));
        }

        using (var again = await admin.UploadAsync(image, ct))
        {
            again.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
        }

        foreach (var (variant, _, relativePath) in first.Variants)
        {
            File.Exists(Path.Combine(api.MediaRoot, relativePath))
                .ShouldBeTrue($"o reenvio não regravou o {variant} que faltava (a linha existe, o arquivo não)");
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0024:H23-02")]
    [Trait("Kind", "Integration")]
    public async Task CreatingAShowWhileTheCleanupDeletesItsFlyerGives400FlyerNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, race) = await FactoryAsync(ct);
        await using var _ = api;
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var __ = admin;
        var (owner, _, producer) = await api.ProducerAsync(admin, "produtor.h2302@subsolo.test", "Peste Produções", ct);
        using var ___ = owner;
        var flyerId = await owner.FlyerAsync(ct, seed: 72);
        race.Arm(flyerId);

        using var response = await owner.PostJsonAsync(
            ShowsApi.Shows, ShowsApi.ShowBody(producer, flyerId, ShowsApi.LocalAt(DateTimeOffset.UtcNow, days: 20)), ct);

        race.Fired.ShouldBe(1, "a limpeza simulada não rodou (o cadastro não conferiu o flyer pelo IFlyerCatalog)");
        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "events.flyer_not_found", ct);
        (await ShowsDatabase.CountAsync(api.ConnectionString, ct)).ShouldBe(0, "o show foi gravado sem o flyer");
    }

    [Fact]
    [Trait("Spec", "SPEC-0024:H23-02")]
    [Trait("Kind", "Integration")]
    public async Task EditingAShowWhileTheCleanupDeletesTheNewFlyerGives400FlyerNotFoundAndKeepsTheOldOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, race) = await FactoryAsync(ct);
        await using var _ = api;
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var __ = admin;
        var (owner, _, producer) = await api.ProducerAsync(admin, "produtor.h2302b@subsolo.test", "Caos Produções", ct);
        using var ___ = owner;
        var oldFlyer = await owner.FlyerAsync(ct, seed: 73);
        var startsLocal = ShowsApi.LocalAt(DateTimeOffset.UtcNow, days: 22);
        var (id, _) = await owner.CreateShowAsync(ShowsApi.ShowBody(producer, oldFlyer, startsLocal), ct);
        var newFlyer = await owner.FlyerAsync(ct, seed: 74);
        race.Arm(newFlyer);

        using var response = await owner.SendJsonAsync(HttpMethod.Put, ShowsApi.Show(id), ShowsApi.ShowBody(producer, newFlyer, startsLocal), ct);

        race.Fired.ShouldBe(1, "a limpeza simulada não rodou (a edição não conferiu o flyer novo pelo IFlyerCatalog)");
        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "events.flyer_not_found", ct);
        (await ShowsDatabase.ShowsAsync(api.ConnectionString, ct)).ShouldHaveSingleItem().FlyerId
            .ShouldBe(oldFlyer, "a edição recusada trocou o flyer do show");
    }

    [Fact]
    [Trait("Spec", "SPEC-0024:H23-02")]
    [Trait("Kind", "Integration")]
    public async Task UploadingAgainWhileTheCleanupDeletesTheRowProcessesAgainInsteadOf500()
    {
        var ct = TestContext.Current.CancellationToken;
        var (api, race) = await FactoryAsync(ct);
        await using var _ = api;
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var __ = admin;
        var image = TestImages.Jpeg(600, 900, seed: 75);
        FlyerUploaded first;
        using (var response = await admin.UploadAsync(image, ct))
        {
            first = await FlyerUploaded.ReadAsync(response, HttpStatusCode.Created, ct);
        }

        race.Arm(first.FlyerId);
        using var again = await admin.UploadAsync(image, ct);
        var body = await again.Content.ReadAsStringAsync(ct);

        race.Fired.ShouldBe(1, "a limpeza simulada não rodou (o envio não leu o flyer existente pelo IFlyerRepository)");
        again.StatusCode.ShouldBeOneOf(
            [HttpStatusCode.OK, HttpStatusCode.Created],
            $"o reenvio com a linha apagada no meio deu {(int)again.StatusCode}: {FlyersApiClient.Excerpt(body)}");
        (await FlyersDatabase.FlyersAsync(api.ConnectionString, ct)).Select(row => row.Id)
            .ShouldContain(first.FlyerId, "o reenvio não gravou de novo a linha do flyer apagado no meio");
        foreach (var (variant, _, relativePath) in first.Variants)
        {
            File.Exists(Path.Combine(api.MediaRoot, relativePath)).ShouldBeTrue($"o reenvio não regravou o {variant}");
        }
    }
}
