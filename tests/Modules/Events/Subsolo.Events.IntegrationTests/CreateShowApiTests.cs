using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0023 — <c>POST /api/v1/shows</c> no Subsolo.Api em memória com o PostgreSQL real:
/// <list type="bullet">
/// <item>IT-01 — o dono de um perfil de produtor aprovado, com um flyer enviado, cria o show: 201 <c>{id, slug}</c> com
/// <c>Location</c>, a linha em <c>events.shows</c>, o flyer com <c>attached_at_utc</c>, <c>ShowPublished</c> na outbox
/// (organizador + casa e bandas com perfil), o show na agenda da cidade e em <c>GET /me/shows</c>; p95 de 20 criações
/// ≤ 300 ms;</item>
/// <item>IT-02 — os casos inválidos do UT-02 dão 400 <c>events.invalid_show</c> com o campo, um <c>flyerId</c>
/// desconhecido dá 400 <c>events.flyer_not_found</c> e o mesmo título na mesma data e cidade dá 409
/// <c>events.show_slug_taken</c>, sem gravar nada (nem show nem evento).</item>
/// </list>
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class CreateShowApiTests(PostgresFixture postgres)
{
    private static readonly TimeSpan CreateBudget = TimeSpan.FromMilliseconds(300);

    private async Task<FlyersApiFactory> FactoryAsync(CancellationToken ct) =>
        new(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

    [Fact]
    [Trait("Spec", "SPEC-0023:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task TheOwnerCreatesAShowThatGoesToTheAgendaWithTheFlyerAttachedAndShowPublishedInTheOutbox()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var _ = admin;
        var (owner, ownerId, producer) = await api.ProducerAsync(admin, "produtor.it01@subsolo.test", "Peste Produções", ct);
        using var __ = owner;
        var venue = await admin.OwnerlessAsync("venue", "Porão do Rock", ct);
        var band = await admin.OwnerlessAsync("band", "Podridão", ct);
        var flyerId = await owner.FlyerAsync(ct);
        var startsLocal = ShowsApi.LocalAt(DateTimeOffset.UtcNow, days: 27);

        var (id, slug) = await owner.CreateShowAsync(
            ShowsApi.ShowBody(
                producer,
                flyerId,
                startsLocal,
                venue: ShowsApi.WithProfile(venue),
                lineup: [ShowsApi.WithProfile(band), ShowsApi.Named("Cova Rasa")]),
            ct);

        slug.ShouldBe($"noite-da-peste-ii-{ShowsApi.DateOf(startsLocal)}");
        var row = (await ShowsDatabase.ShowsAsync(api.ConnectionString, ct)).ShouldHaveSingleItem("events.shows sem a linha do show");
        row.Id.ShouldBe(id);
        row.Slug.ShouldBe(slug);
        row.City.ShouldBe(ShowsApi.City);
        row.Title.ShouldBe("Noite da Peste II");
        row.StartsAtUtc.ShouldBe(ShowsApi.UtcOf(startsLocal));
        row.OrganizerProfileId.ShouldBe(producer);
        row.VenueProfileId.ShouldBe(venue);
        row.VenueName.ShouldBe("Porão do Rock", "o nome da casa com perfil vem do perfil");
        row.Lineup.ShouldContain(band.ToString(), Case.Insensitive);
        row.Lineup.ShouldContain("Cova Rasa", Case.Sensitive);
        row.Styles.ShouldBe([ShowsApi.Style]);
        row.Price.ShouldBe("R$ 30 / R$ 40 na hora");
        row.TicketUrl.ShouldBe("https://ingressos.example/noite-da-peste-ii");
        row.FlyerId.ShouldBe(flyerId);
        row.Status.ShouldBe("scheduled");
        row.CreatedBy.ShouldBe(ownerId);
        (await FlyersDatabase.FlyersAsync(api.ConnectionString, ct)).Single(flyer => flyer.Id == flyerId)
            .AttachedAtUtc.ShouldNotBeNull("o flyer não foi ligado ao show (attached_at_utc nulo)");

        var published = (await ShowsDatabase.OutboxContentsAsync(api.ConnectionString, "ShowPublished", ct))
            .ShouldHaveSingleItem("ShowPublished não foi para a outbox (ou foi mais de uma vez)");
        published.ShouldContain(id.ToString(), Case.Insensitive, published);
        foreach (var profile in new[] { producer, venue, band })
        {
            published.ShouldContain(profile.ToString(), Case.Insensitive, $"ShowPublished sem o perfil {profile}: {published}");
        }

        var agenda = await admin.AgendaShowsAsync(ShowsApi.City, ct);
        var card = agenda.ById(id).ShouldNotBeNull($"o show não está na agenda da cidade: {agenda}");
        card.GetProperty("slug").GetString().ShouldBe(slug);
        card.GetProperty("title").GetString().ShouldBe("Noite da Peste II");
        card.GetProperty("startsAt").GetDateTimeOffset().ShouldBe(ShowsApi.UtcOf(startsLocal));
        card.GetProperty("venueName").GetString().ShouldBe("Porão do Rock");
        card.GetProperty("lineup").EnumerateArray().Select(item => item.GetString()).ShouldBe(["Podridão", "Cova Rasa"]);
        card.GetProperty("status").GetString().ShouldBe("scheduled");
        card.GetProperty("flyerThumbUrl").GetString().ShouldBe($"/media/flyers/{flyerId}-thumb.webp");

        using var mine = await owner.GetAsync(ShowsApi.MyShowsFor("upcoming"), ct);
        (await mine.ShouldBeJsonAsync(HttpStatusCode.OK, ct)).ById(id).ShouldNotBeNull("o show não aparece em Meus shows");
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task TwentyCreationsHaveP95UnderThreeHundredMilliseconds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var _ = admin;
        var (owner, _, producer) = await api.ProducerAsync(admin, "produtor.p95@subsolo.test", "Caos Produções", ct);
        using var __ = owner;
        var flyerId = await owner.FlyerAsync(ct);
        var startsLocal = ShowsApi.LocalAt(DateTimeOffset.UtcNow, days: 30);
        // Aquecimento (JIT, pool de conexões): fora da medida.
        await owner.CreateShowAsync(ShowsApi.ShowBody(producer, flyerId, startsLocal, title: "Aquecimento"), ct);

        var durations = new List<TimeSpan>();
        for (var i = 1; i <= 20; i++)
        {
            var body = ShowsApi.ShowBody(producer, flyerId, startsLocal, title: $"Noite da Peste {i:00}");
            var token = await owner.CsrfTokenAsync(ct);
            using var content = System.Net.Http.Json.JsonContent.Create(body);
            var watch = Stopwatch.StartNew();
            using var response = await owner.SendAsync(HttpMethod.Post, ShowsApi.Shows, ct, content, token);
            durations.Add(watch.Elapsed);
            await response.ShouldBeJsonAsync(HttpStatusCode.Created, ct);
        }

        var p95 = Timing.Percentile(durations, 95);
        p95.ShouldBeLessThanOrEqualTo(
            CreateBudget,
            $"p95 de 20 criações = {p95.TotalMilliseconds:F0} ms (ms: {string.Join(", ", durations.Select(d => d.TotalMilliseconds.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)))})");
    }

    public static TheoryData<string, string, string> InvalidCases()
    {
        var data = new TheoryData<string, string, string>
        {
            { "título de 1 caractere", """{"title":"x"}""", "title" },
            { "título de 101 caracteres", $$"""{"title":"{{new string('x', 101)}}"}""", "title" },
            { "data no passado", """{"startsLocal":"2020-01-10T22:00"}""", "startsLocal" },
            { "line-up vazio", """{"lineup":[]}""", "lineup" },
            { "line-up com 16", $$"""{"lineup":[{{string.Join(",", Enumerable.Range(1, 16).Select(i => $$"""{"name":"Banda {{i}}"}"""))}}]}""", "lineup" },
            { "sem estilo", """{"styles":[]}""", "styles" },
            { "6 estilos", """{"styles":["death","black","grind","doom","thrash","death"]}""", "styles" },
            { "estilo inexistente", """{"styles":["axe"]}""", "styles" },
            { "cidade inexistente", """{"city":"gotham"}""", "city" },
            { "preço de 41", $$"""{"price":"{{new string('9', 41)}}"}""", "price" },
            { "link http", """{"ticketUrl":"http://x"}""", "ticketUrl" },
            { "casa sem perfil nem nome", """{"venue":{}}""", "venue" },
        };
        return data;
    }

    [Theory]
    [Trait("Spec", "SPEC-0023:IT-02")]
    [Trait("Kind", "Integration")]
    [MemberData(nameof(InvalidCases))]
    public async Task AnInvalidFieldReturns400InvalidShowWithTheFieldAndSavesNothing(string _, string overrides, string field)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var __ = admin;
        var organizer = await admin.OwnerlessAsync("producer", "Porão Coletivo", ct);
        var flyerId = await admin.FlyerAsync(ct);
        var body = ShowsApi.ShowBody(organizer, flyerId, ShowsApi.LocalAt(DateTimeOffset.UtcNow, days: 20));
        using (var document = JsonDocument.Parse(overrides))
        {
            foreach (var property in document.RootElement.EnumerateObject())
            {
                body[property.Name] = property.Value.Clone();
            }
        }

        using var response = await admin.PostJsonAsync(ShowsApi.Shows, body, ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "events.invalid_show", ct);
        problem.ShouldHaveFieldErrorStartingWith(field);
        (await ShowsDatabase.CountAsync(api.ConnectionString, ct)).ShouldBe(0, "o show inválido foi gravado");
        (await ShowsDatabase.OutboxContentsAsync(api.ConnectionString, "Show", ct)).ShouldBeEmpty("o show inválido gerou evento");
        (await FlyersDatabase.FlyersAsync(api.ConnectionString, ct)).Single().AttachedAtUtc.ShouldBeNull("o flyer foi ligado a um show inválido");
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task AnUnknownFlyerIs400FlyerNotFoundAndARepeatedTitleOnTheSameDateIs409SlugTaken()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var _ = admin;
        var organizer = await admin.OwnerlessAsync("producer", "Porão Coletivo", ct);
        var flyerId = await admin.FlyerAsync(ct);
        var startsLocal = ShowsApi.LocalAt(DateTimeOffset.UtcNow, days: 20);

        using (var unknown = await admin.PostJsonAsync(ShowsApi.Shows, ShowsApi.ShowBody(organizer, new string('f', 64), startsLocal), ct))
        {
            await unknown.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "events.flyer_not_found", ct);
        }

        (await ShowsDatabase.CountAsync(api.ConnectionString, ct)).ShouldBe(0, "o show com flyer desconhecido foi gravado");

        var (id, _) = await admin.CreateShowAsync(ShowsApi.ShowBody(organizer, flyerId, startsLocal), ct);
        // Mesmo título e mesma data (outra hora): mesmo slug na cidade.
        using (var repeated = await admin.PostJsonAsync(
                   ShowsApi.Shows, ShowsApi.ShowBody(organizer, flyerId, ShowsApi.LocalAt(DateTimeOffset.UtcNow, days: 20, hour: 19)), ct))
        {
            await repeated.ShouldBeProblemAsync(HttpStatusCode.Conflict, "events.show_slug_taken", ct);
        }

        (await ShowsDatabase.ShowsAsync(api.ConnectionString, ct)).ShouldHaveSingleItem("o show repetido foi gravado").Id.ShouldBe(id);
        (await ShowsDatabase.OutboxContentsAsync(api.ConnectionString, "ShowPublished", ct)).Count.ShouldBe(1, "o show repetido gerou evento");
    }
}
