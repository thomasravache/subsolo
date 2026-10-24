using System.Globalization;
using Subsolo.Engagement.UnitTests.Infrastructure;
using Subsolo.Modules.Engagement.Application;
using Subsolo.Modules.Engagement.Application.Features.DailyDigest;
using Subsolo.Modules.Engagement.Application.Features.NotifyNewShow;
using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Engagement.UnitTests;

/// <summary>
/// SPEC-0031 UT-02 — o montador do resumo diário (<see cref="DigestBuilder"/>) e o consumidor do show novo
/// (<see cref="NotifyNewShow"/>), sobre os follows em memória e o relógio fixo (<see cref="EngagementHarness.Start"/>,
/// 2026-10-19 09h00 em São Paulo):
/// <list type="bullet">
/// <item>dados follows de quatro fãs e os shows publicados nas últimas 24 h — dois em Curitiba (um com a banda A), um
/// cancelado, um que já começou e 22 de São Paulo —, cada fã recebe um aviso <c>digest</c> só com os shows dele, sem
/// repetição (quem segue a cidade e a banda vê o show uma vez), pela data, uma linha
/// "{dd/MM} · {Lineup} · {VenueName}, {CityName}" por show, até 20 e "e mais {n} na agenda"; título "Shows novos de quem
/// você segue", url "/" e chave "digest:{aaaa-mm-dd em São Paulo}"; o cancelado e o já começado ficam de fora, e quem
/// não tem show novo não recebe nada;</item>
/// <item>show novo sem nenhum seguidor: nada é enfileirado (Mapa de Comportamentos, "Sem seguidores").</item>
/// </list>
/// </summary>
public sealed class DailyDigestTests
{
    private const string DigestKey = "digest:2026-10-19";

    private static readonly Guid BandA = Guid.CreateVersion7();
    private static readonly Guid BandB = Guid.CreateVersion7();

    private static readonly DateTimeOffset PublishedAt = EngagementHarness.Start.AddHours(-2);

    private sealed record Scenario(
        EngagementHarness Harness,
        IReadOnlyList<PublishedShow> Published,
        Guid CityFan,
        Guid BandFan,
        Guid SaoPauloFan,
        Guid NobodysFan);

    /// <summary>
    /// Os follows e os shows publicados (todos há 2 h): em Curitiba, "Necrose" no Porão (22/10, 20h) e "Cova Rasa + Peste
    /// Negra" no Basement com a banda A (23/10, 22h), um cancelado e um que começou ontem; 22 em São Paulo (20/10 a 10/11).
    /// </summary>
    private static Scenario Arrange()
    {
        var harness = new EngagementHarness();
        harness.Profiles.AddApproved(BandA, "Cova Rasa", "cova-rasa");
        harness.Profiles.AddApproved(BandB, "Banda Sem Show", "banda-sem-show");
        var published = new List<PublishedShow>
        {
            Publish(harness, "curitiba", "Curitiba", ["Necrose"], "Porão", new DateTimeOffset(2026, 10, 22, 23, 0, 0, TimeSpan.Zero)),
            Publish(harness, "curitiba", "Curitiba", ["Cova Rasa", "Peste Negra"], "Basement", new DateTimeOffset(2026, 10, 24, 1, 0, 0, TimeSpan.Zero), profiles: [BandA]),
            Publish(harness, "curitiba", "Curitiba", ["Cancelada"], "Porão", new DateTimeOffset(2026, 10, 25, 23, 0, 0, TimeSpan.Zero), status: "cancelled", profiles: [BandA]),
            Publish(harness, "curitiba", "Curitiba", ["Já Começou"], "Porão", EngagementHarness.Start.AddHours(-1), profiles: [BandA]),
        };
        for (var day = 1; day <= 22; day++)
        {
            published.Add(Publish(
                harness,
                "sao-paulo",
                "São Paulo",
                [string.Create(CultureInfo.InvariantCulture, $"Banda SP {day:00}")],
                "Hangar",
                new DateTimeOffset(EngagementHarness.Start.Date.AddDays(day).AddHours(23), TimeSpan.Zero)));
        }

        var cityFan = Guid.CreateVersion7();
        var bandFan = Guid.CreateVersion7();
        var saoPauloFan = Guid.CreateVersion7();
        var nobodysFan = Guid.CreateVersion7();
        harness.AddFollow(cityFan, FollowTarget.City("curitiba"));
        harness.AddFollow(cityFan, FollowTarget.Profile(BandA));
        harness.AddFollow(bandFan, FollowTarget.Profile(BandA));
        harness.AddFollow(saoPauloFan, FollowTarget.City("sao-paulo"));
        harness.AddFollow(nobodysFan, FollowTarget.Profile(BandB));
        return new Scenario(harness, published, cityFan, bandFan, saoPauloFan, nobodysFan);
    }

    private static PublishedShow Publish(
        EngagementHarness harness,
        string citySlug,
        string cityName,
        IReadOnlyList<string> lineup,
        string venue,
        DateTimeOffset startsAtUtc,
        string status = "scheduled",
        IReadOnlyList<Guid>? profiles = null)
    {
        var id = Guid.CreateVersion7();
        harness.Catalog.AddSummary(new ShowSummary(
            id, $"show-{id:N}", citySlug, cityName, "America/Sao_Paulo", lineup, venue, startsAtUtc, status));
        var row = new PublishedShow(id, citySlug, profiles ?? [], PublishedAt);
        harness.PublishedShows.AddAsync(row, CancellationToken.None).GetAwaiter().GetResult();
        return row;
    }

    private static async Task<IReadOnlyList<DigestNotice>> BuildAsync(Scenario scenario) =>
        await scenario.Harness.Create<DigestBuilder>().BuildAsync(scenario.Published, TestContext.Current.CancellationToken);

    private static string[] Lines(DigestNotice notice) => notice.Notice.Body.Split('\n');

    [Fact]
    [Trait("Spec", "SPEC-0031:UT-02")]
    public async Task EachFanGetsOneDigestWithTheContractTitleUrlAndKey()
    {
        var scenario = Arrange();

        var notices = await BuildAsync(scenario);

        notices.Select(notice => notice.UserId).ShouldBe(
            [scenario.CityFan, scenario.BandFan, scenario.SaoPauloFan], ignoreOrder: true, "um aviso por fã com show novo");
        notices.ShouldAllBe(notice =>
            notice.Notice.Kind == "digest"
            && notice.Notice.Title == "Shows novos de quem você segue"
            && notice.Notice.Url == "/"
            && notice.Notice.DedupeKey == DigestKey);
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:UT-02")]
    public async Task AFanFollowingTheCityAndTheBandSeesEachShowOnceByDateWithoutCancelledOrStarted()
    {
        var scenario = Arrange();

        var notices = await BuildAsync(scenario);

        Lines(notices.Single(notice => notice.UserId == scenario.CityFan)).ShouldBe(
        [
            "22/10 · Necrose · Porão, Curitiba",
            "23/10 · Cova Rasa + Peste Negra · Basement, Curitiba",
        ]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:UT-02")]
    public async Task AFanFollowingOnlyTheBandSeesOnlyItsShow()
    {
        var scenario = Arrange();

        var notices = await BuildAsync(scenario);

        Lines(notices.Single(notice => notice.UserId == scenario.BandFan)).ShouldBe(
            ["23/10 · Cova Rasa + Peste Negra · Basement, Curitiba"]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:UT-02")]
    public async Task MoreThanTwentyShowsListTheFirstTwentyAndHowManyMoreAreInTheAgenda()
    {
        var scenario = Arrange();

        var notices = await BuildAsync(scenario);

        var lines = Lines(notices.Single(notice => notice.UserId == scenario.SaoPauloFan));
        var expected = Enumerable.Range(1, DigestBuilder.MaxShows)
            .Select(day => string.Create(
                CultureInfo.InvariantCulture,
                $"{EngagementHarness.Start.Date.AddDays(day):dd/MM} · Banda SP {day:00} · Hangar, São Paulo"))
            .Append("e mais 2 na agenda")
            .ToList();
        lines.ShouldBe(expected);
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:UT-02")]
    public async Task AFanWithoutNewShowsGetsNoDigest()
    {
        var scenario = Arrange();

        var notices = await BuildAsync(scenario);

        notices.Select(notice => notice.UserId).ShouldNotContain(scenario.NobodysFan);
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:UT-02")]
    public async Task ANewShowWithoutFollowersEnqueuesNothing()
    {
        var harness = new EngagementHarness();
        var summary = harness.Catalog.AddSummary(new ShowSummary(
            Guid.CreateVersion7(),
            "noite-sem-ninguem",
            "curitiba",
            "Curitiba",
            "America/Sao_Paulo",
            ["Necrose"],
            "Porão",
            EngagementHarness.Start.AddDays(5),
            "scheduled"));

        await harness.Create<NotifyNewShow>().Handle(
            new ShowPublishedIntegrationEvent(summary.ShowId, summary.CitySlug, [], summary.StartsAtUtc)
            {
                Id = Guid.CreateVersion7(),
                OccurredOnUtc = EngagementHarness.Start.UtcDateTime,
            },
            TestContext.Current.CancellationToken);

        harness.Notifications.Enqueued.Where(call => call.UserIds.Count > 0)
            .ShouldBeEmpty("um show sem seguidores enfileirou aviso");
    }
}
