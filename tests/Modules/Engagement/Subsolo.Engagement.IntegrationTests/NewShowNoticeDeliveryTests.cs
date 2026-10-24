using System.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0031 IT-01 (§5: do evento ao <c>EnqueueAsync</c> em ≤ 5 s para 10.000 seguidores) — no Subsolo.Api em memória com
/// o PostgreSQL real e o relógio fixo: 10.000 seguidores da cidade de desenvolvimento e 300 de uma banda (100 deles
/// também seguem a cidade), gravados direto, com as preferências e os aparelhos do Notifications; o admin cadastra pela API
/// um show na cidade com a banda no line-up. O <c>ShowPublished</c> é consumido pelo Engagement e
/// <c>notifications.deliveries</c> recebe uma entrega push <c>new_show</c> por fã com aparelho e o show novo ligado —
/// nenhuma para quem desligou o aviso ou não tem aparelho, nenhuma repetida —, em ≤ 5 s da criação do show; o aviso
/// gravado tem o título "Show novo em {cidade}" e a url "/{cidade}/{slug}" do §6. O job de entregas fica pausado (as
/// entregas ficam pendentes; o envio é do SPEC-0030).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class NewShowNoticeDeliveryTests(PostgresFixture postgres)
{
    private const int CityFollowers = 10_000;

    private const int BandFollowers = 300;

    private const int Overlap = 100;

    private const int PushOff = 200;

    private const int WithoutDevice = 200;

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Spec", "SPEC-0031:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task ANewShowReachesEveryFollowerWithPushOnWithinFiveSeconds()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = new EngagementApiFactory(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        using var __ = admin;
        var band = await admin.CreateApprovedProfileAsync("Cova Rasa", ct);

        var cityFans = Enumerable.Range(0, CityFollowers).Select(_ => Guid.CreateVersion7()).ToList();
        var bandFans = cityFans.Take(Overlap).Concat(Enumerable.Range(0, BandFollowers - Overlap).Select(_ => Guid.CreateVersion7())).ToList();
        var everyone = cityFans.Union(bandFans).ToList();
        var pushOff = everyone.Skip(Overlap).Take(PushOff).ToHashSet();
        var withoutDevice = everyone.Skip(Overlap + PushOff).Take(WithoutDevice).ToHashSet();
        await EngagementDatabase.SeedAsync(api.ConnectionString, cityFans, "city", EngagementApiFactory.City, ct);
        await EngagementDatabase.SeedAsync(api.ConnectionString, bandFans, "profile", band.ToString(), ct);
        await EngagementDatabase.AnalyzeAsync(api.ConnectionString, ct);
        await NoticesDatabase.SeedPreferencesAsync(
            api.ConnectionString, everyone.Select(id => (id, NewShowPush: !pushOff.Contains(id), DailyDigestEmail: false)), ct);
        await NoticesDatabase.SeedSubscriptionsAsync(api.ConnectionString, everyone.Where(id => !withoutDevice.Contains(id)), ct);
        await QuartzJobs.PauseAsync(api, QuartzJobs.NoticeDeliveries, ct);
        var expected = everyone.Where(id => !pushOff.Contains(id) && !withoutDevice.Contains(id)).ToHashSet();

        var watch = Stopwatch.StartNew();
        var show = await admin.CreateShowAsync("Noite da Peste II", clock.GetUtcNow(), days: 10, ct, band: band);
        var key = $"new_show:{show.Id}";
        var reached = await Timing.EventuallyAsync(
            async () => await PushDeliveriesAsync(api, key, ct) >= expected.Count,
            TimeSpan.FromSeconds(60),
            ct);
        var elapsed = watch.Elapsed;

        reached.ShouldBeTrue($"o show novo não virou {expected.Count} entregas push (há {await PushDeliveriesAsync(api, key, ct)})");
        elapsed.ShouldBeLessThanOrEqualTo(Budget, $"do evento às {expected.Count} entregas levou {elapsed.TotalMilliseconds:F0} ms");
        var deliveries = (await NoticesDatabase.DeliveriesAsync(api.ConnectionString, ct)).Where(row => row.DedupeKey == key).ToList();
        deliveries.ShouldAllBe(row => row.Kind == "new_show" && row.Channel == "push");
        deliveries.Select(row => row.UserId).ShouldBeUnique("entrega repetida para o mesmo fã");
        deliveries.Select(row => row.UserId).ToHashSet().SetEquals(expected)
            .ShouldBeTrue("as entregas não são exatamente dos fãs com aparelho e o show novo ligado");

        var notice = (await NoticesDatabase.NoticesAsync(api.ConnectionString, ct)).Single(row => row.DedupeKey == key);
        notice.Kind.ShouldBe("new_show");
        notice.Title.ShouldBe($"Show novo em {NoticeRequests.CityName}");
        notice.Url.ShouldBe($"/{EngagementApiFactory.City}/{show.Slug}");
        notice.Body.ShouldEndWith($" · {NoticeRequests.Venue}");
        notice.Body.ShouldStartWith("Cova Rasa · ");
    }

    private static async Task<long> PushDeliveriesAsync(EngagementApiFactory api, string key, CancellationToken ct) =>
        (await NoticesDatabase.DeliveriesAsync(api.ConnectionString, ct)).LongCount(row => row.DedupeKey == key && row.Channel == "push");
}
