using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Npgsql;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;
using Testcontainers.PostgreSql;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0024 — <c>GET /api/v1/cities/{city}/shows/{slug}</c> (público, sem login) no Subsolo.Api em memória com o
/// PostgreSQL real:
/// <list type="bullet">
/// <item>IT-01 — um show agendado (casa e uma banda com perfil, outra banda por nome, flyer enviado) e outro cancelado:
/// 200 com o corpo do §6 (<c>{id, slug, title, city: {slug, name, timeZone}, startsAt (UTC), venue: {name,
/// profileSlug|null}, lineup: [{name, profileSlug|null}], styles: [{slug, name}], price, ticketUrl|null, status, flyer:
/// {thumbUrl, fullUrl, ogUrl, width, height}}</c>), as URLs das variantes e a situação; sem dado de quem cadastrou; p95
/// de 20 chamadas ≤ 150 ms;</item>
/// <item>IT-03 (parte da API) — slug desconhecido → 404 <c>events.show_not_found</c>; cidade desconhecida → 404
/// <c>events.city_not_found</c>;</item>
/// <item>IT-04 (parte da API) — com o PostgreSQL (container próprio do teste) pausado → 503 em ProblemDetails, sem
/// detalhe da exceção.</item>
/// </list>
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ShowDetailApiTests(PostgresFixture postgres)
{
    private static readonly TimeSpan DetailBudget = TimeSpan.FromMilliseconds(150);

    private async Task<FlyersApiFactory> FactoryAsync(CancellationToken ct) =>
        new(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

    [Fact]
    [Trait("Spec", "SPEC-0024:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task AnAnonymousVisitorGetsTheScheduledAndTheCancelledShowWithTheContractBody()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var _ = admin;
        const string ownerEmail = "produtor.detalhe@subsolo.test";
        var (owner, ownerId, producer) = await api.ProducerAsync(admin, ownerEmail, "Peste Produções", ct);
        using var __ = owner;
        var venue = await admin.OwnerlessAsync("venue", "Porão do Rock", ct);
        var band = await admin.OwnerlessAsync("band", "Podridão", ct);
        var flyerId = await owner.FlyerAsync(ct);
        var startsLocal = ShowsApi.LocalAt(DateTimeOffset.UtcNow, days: 21);
        var (scheduledId, scheduledSlug) = await owner.CreateShowAsync(
            ShowsApi.ShowBody(
                producer,
                flyerId,
                startsLocal,
                venue: ShowsApi.WithProfile(venue),
                lineup: [ShowsApi.WithProfile(band), ShowsApi.Named("Cova Rasa")]),
            ct);
        var (cancelledId, cancelledSlug) = await owner.CreateShowAsync(
            ShowsApi.ShowBody(producer, flyerId, startsLocal, title: "Sepulcro Fest", ticketUrl: null),
            ct);
        using (var cancel = await owner.PostJsonAsync(ShowsApi.Status(cancelledId), new { status = "cancelled" }, ct))
        {
            cancel.StatusCode.ShouldBe(HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync(ct));
        }

        using var anonymous = api.CreateFlyersClient();

        using var response = await anonymous.GetAsync(ShowsApi.Detail(ShowsApi.City, scheduledSlug), ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {ShowsApi.Detail(ShowsApi.City, scheduledSlug)}: {FlyersApiClient.Excerpt(raw)}");
        using var document = JsonDocument.Parse(raw);
        var show = document.RootElement;
        show.GetProperty("id").GetGuid().ShouldBe(scheduledId);
        show.GetProperty("slug").GetString().ShouldBe(scheduledSlug);
        show.GetProperty("title").GetString().ShouldBe("Noite da Peste II");
        var city = show.GetProperty("city");
        city.GetProperty("slug").GetString().ShouldBe(ShowsApi.City);
        city.GetProperty("name").GetString().ShouldBe("Cidade Exemplo");
        city.GetProperty("timeZone").GetString().ShouldBe("America/Sao_Paulo");
        show.GetProperty("startsAt").GetDateTimeOffset().ShouldBe(ShowsApi.UtcOf(startsLocal));
        show.GetProperty("startsAt").GetString().ShouldNotBeNull().ShouldEndWith("Z", Case.Sensitive, "startsAt não está em UTC");

        var venuePart = show.GetProperty("venue");
        venuePart.GetProperty("name").GetString().ShouldBe("Porão do Rock");
        venuePart.GetProperty("profileSlug").GetString().ShouldNotBeNullOrWhiteSpace("a casa com perfil sem profileSlug");
        var lineup = show.GetProperty("lineup").EnumerateArray().ToList();
        lineup.Select(item => item.GetProperty("name").GetString()).ShouldBe(["Podridão", "Cova Rasa"]);
        lineup[0].GetProperty("profileSlug").GetString().ShouldNotBeNullOrWhiteSpace("a banda com perfil sem profileSlug");
        lineup[1].GetProperty("profileSlug").ValueKind.ShouldBe(JsonValueKind.Null, "a banda sem perfil tem profileSlug");

        var styles = show.GetProperty("styles").EnumerateArray().ToList();
        styles.ShouldHaveSingleItem().GetProperty("slug").GetString().ShouldBe(ShowsApi.Style);
        styles[0].GetProperty("name").GetString().ShouldNotBeNullOrWhiteSpace("estilo sem nome");
        show.GetProperty("price").GetString().ShouldBe("R$ 30 / R$ 40 na hora");
        show.GetProperty("ticketUrl").GetString().ShouldBe("https://ingressos.example/noite-da-peste-ii");
        show.GetProperty("status").GetString().ShouldBe("scheduled");

        var flyer = show.GetProperty("flyer");
        flyer.GetProperty("thumbUrl").GetString().ShouldBe($"/media/flyers/{flyerId}-thumb.webp");
        flyer.GetProperty("fullUrl").GetString().ShouldBe($"/media/flyers/{flyerId}-full.webp");
        flyer.GetProperty("ogUrl").GetString().ShouldBe($"/media/flyers/{flyerId}-og.jpg");
        // O flyer de teste tem 300×400 (o full não aumenta imagem mais estreita que 1080 px): as medidas do full.
        flyer.GetProperty("width").GetInt32().ShouldBe(300, "largura do full");
        flyer.GetProperty("height").GetInt32().ShouldBeInRange(399, 401, "altura do full");

        // Privacidade (§5): nada de quem cadastrou nem de conta.
        foreach (var hidden in new[] { "createdBy", "organizerProfileId", "userId", "email" })
        {
            show.TryGetProperty(hidden, out var _).ShouldBeFalse($"o detalhe público expõe {hidden}: {raw}");
        }

        raw.ShouldNotContain(ownerId.ToString(), Case.Insensitive, "o detalhe público expõe o id de quem cadastrou");
        raw.ShouldNotContain(ownerEmail, Case.Insensitive, "o detalhe público expõe o e-mail de quem cadastrou");

        using var cancelled = await anonymous.GetAsync(ShowsApi.Detail(ShowsApi.City, cancelledSlug), ct);
        var cancelledBody = await cancelled.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        cancelledBody.GetProperty("id").GetGuid().ShouldBe(cancelledId);
        cancelledBody.GetProperty("status").GetString().ShouldBe("cancelled", "o show cancelado continua com página, com a situação");
        cancelledBody.GetProperty("ticketUrl").ValueKind.ShouldBe(JsonValueKind.Null, "show sem link de ingresso");
    }

    [Fact]
    [Trait("Spec", "SPEC-0024:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task TwentyDetailCallsHaveP95UnderOneHundredFiftyMilliseconds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var _ = admin;
        var (owner, _, producer) = await api.ProducerAsync(admin, "produtor.detalhe.p95@subsolo.test", "Caos Produções", ct);
        using var __ = owner;
        var flyerId = await owner.FlyerAsync(ct);
        var (_, slug) = await owner.CreateShowAsync(
            ShowsApi.ShowBody(producer, flyerId, ShowsApi.LocalAt(DateTimeOffset.UtcNow, days: 30)), ct);
        using var anonymous = api.CreateFlyersClient();
        var route = ShowsApi.Detail(ShowsApi.City, slug);
        // Aquecimento (JIT, pool de conexões): fora da medida.
        using (var warmUp = await anonymous.GetAsync(route, ct))
        {
            warmUp.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {route}: {FlyersApiClient.Excerpt(await warmUp.Content.ReadAsStringAsync(ct))}");
        }

        var durations = new List<TimeSpan>();
        for (var i = 0; i < 20; i++)
        {
            var watch = Stopwatch.StartNew();
            using var response = await anonymous.GetAsync(route, ct);
            watch.Stop();
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            durations.Add(watch.Elapsed);
        }

        Timing.Percentile(durations, 95).ShouldBeLessThanOrEqualTo(
            DetailBudget,
            $"p95 de GET {route} acima de {DetailBudget.TotalMilliseconds} ms: {string.Join(", ", durations.Select(d => $"{d.TotalMilliseconds:0} ms"))}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0024:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task AnUnknownSlugOrCityGives404WithTheContractCodes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        using var anonymous = api.CreateFlyersClient();

        using (var response = await anonymous.GetAsync(ShowsApi.Detail(ShowsApi.City, "show-que-nao-existe-2026-01-01"), ct))
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "events.show_not_found", ct);
        }

        using (var response = await anonymous.GetAsync(ShowsApi.Detail("cidade-inexistente", "show-que-nao-existe-2026-01-01"), ct))
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "events.city_not_found", ct);
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0024:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task WithoutTheDatabaseTheDetailAnswers503ProblemDetailsWithoutDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var container = new PostgreSqlBuilder(PostgresFixture.Image).Build();
        await container.StartAsync(ct);
        var database = await FlyersApiFactory.IsolatedDatabaseAsync(container.GetConnectionString(), ct);
        // Prazos curtos: com o banco pausado, a conexão e o comando desistem logo.
        var connectionString = new NpgsqlConnectionStringBuilder(database) { Timeout = 3, CommandTimeout = 3 }.ConnectionString;
        await using var api = new FlyersApiFactory(connectionString);
        using var anonymous = api.CreateFlyersClient();
        using (var started = await anonymous.GetAsync(ShowsApi.Agenda(ShowsApi.City), ct))
        {
            started.StatusCode.ShouldBe(HttpStatusCode.OK, "a API não subiu com o banco no ar");
        }

        await container.PauseAsync(ct);
        HttpResponseMessage response;
        string body;
        try
        {
            response = await anonymous.GetAsync(ShowsApi.Detail(ShowsApi.City, "show-sem-banco"), ct);
            body = await response.Content.ReadAsStringAsync(ct);
        }
        finally
        {
            await container.UnpauseAsync(ct);
        }

        using (response)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable, FlyersApiClient.Excerpt(body));
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json", body);
            body.ShouldNotContain("Npgsql", Case.Insensitive, "detalhe da exceção vazou");
            body.ShouldNotContain("Exception", Case.Insensitive, "detalhe da exceção vazou");
        }
    }
}
