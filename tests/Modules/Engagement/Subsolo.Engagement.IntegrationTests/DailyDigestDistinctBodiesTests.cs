using System.Diagnostics;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0032 H31-02 (item herdado da revisão da SPEC-0031) — a variante do SPEC-0031:IT-04 com corpos de resumo
/// diferentes: dez bandas com perfil, cada uma com um show novo cadastrado pela API (registrado em
/// <c>published_shows</c> há 1 hora com o perfil dela); 10.000 fãs com o resumo diário ligado, espalhados pelas bandas
/// (o fã <c>i</c> segue as bandas do conjunto <c>(i mod 1023) + 1</c>, em bits: 1.023 combinações, 1.023 corpos). Disparado
/// o job <c>Engagement.DailyDigest</c>, as 10.000 entregas <c>digest</c> por e-mail existem em ≤ 60 s, uma por fã, com
/// 1.023 corpos diferentes. O job de entregas fica pausado.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class DailyDigestDistinctBodiesTests(PostgresFixture postgres)
{
    private const int Fans = 10_000;
    private const int Bands = 10;
    private const int Combinations = (1 << Bands) - 1;

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    [Fact]
    [Trait("Spec", "SPEC-0032:H31-02")]
    [Trait("Kind", "Integration")]
    public async Task TheDigestWithFansSpreadAcrossBandsReachesTenThousandFansWithinOneMinute()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new RunningClock(DateTimeOffset.UtcNow);
        await using var api = new EngagementApiFactory(
            await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock, ReminderRequests.ManualReminderSettings());
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        using var __ = admin;
        await QuartzJobs.PauseAsync(api, QuartzJobs.NoticeDeliveries, ct);
        var now = clock.GetUtcNow();
        var bands = new List<Guid>(Bands);
        var published = new List<PublishedShowRow>(Bands);
        for (var index = 0; index < Bands; index++)
        {
            var band = await admin.CreateApprovedProfileAsync($"Banda Espalhada {index:00}", ct);
            var show = await admin.CreateShowAsync($"Noite Espalhada {index:00}", now, days: 3 + index, ct, band: band);
            bands.Add(band);
            published.Add(new PublishedShowRow(show.Id, EngagementApiFactory.City, [band], now.AddHours(-1)));
        }

        await NoticesDatabase.SeedPublishedShowsAsync(api.ConnectionString, published, ct);
        var fans = Enumerable.Range(0, Fans).Select(_ => Guid.CreateVersion7()).ToList();
        await NoticesDatabase.SeedUsersAsync(api.ConnectionString, fans, id => $"fa-{id:N}@subsolo.test", ct);
        for (var bit = 0; bit < Bands; bit++)
        {
            var followers = fans.Where((_, index) => ((Mask(index) >> bit) & 1) == 1).ToList();
            await EngagementDatabase.SeedAsync(api.ConnectionString, followers, "profile", bands[bit].ToString(), ct);
        }

        await EngagementDatabase.AnalyzeAsync(api.ConnectionString, ct);
        await NoticesDatabase.SeedPreferencesAsync(api.ConnectionString, fans.Select(id => (id, NewShowPush: false, DailyDigestEmail: true)), ct);
        var runs = await QuartzJobs.RunsAsync(api, QuartzJobs.DailyDigest, ct);

        var watch = Stopwatch.StartNew();
        await runs.RunOnceAsync(ct);
        var reached = await Timing.EventuallyAsync(
            async () => await NoticesDatabase.CountDeliveriesAsync(api.ConnectionString, "digest", "email", ct) >= Fans,
            TimeSpan.FromSeconds(120),
            ct);
        var elapsed = watch.Elapsed;

        reached.ShouldBeTrue(
            $"o resumo não virou {Fans} entregas por e-mail (há {await NoticesDatabase.CountDeliveriesAsync(api.ConnectionString, "digest", "email", ct)})");
        elapsed.ShouldBeLessThanOrEqualTo(Budget, $"o resumo com {Combinations} corpos para {Fans} fãs levou {elapsed.TotalSeconds:F1} s");
        var deliveries = (await NoticesDatabase.DeliveriesAsync(api.ConnectionString, ct)).Where(row => row.Kind == "digest").ToList();
        deliveries.Select(row => row.UserId).ShouldBeUnique("resumo repetido para o mesmo fã");
        deliveries.Select(row => row.UserId).ToHashSet().SetEquals(fans).ShouldBeTrue("as entregas não são exatamente dos 10.000 fãs");
        var bodies = (await NoticesDatabase.NoticesAsync(api.ConnectionString, ct))
            .Where(notice => notice.Kind == "digest")
            .Select(notice => notice.Body)
            .ToHashSet(StringComparer.Ordinal);
        bodies.Count.ShouldBe(Combinations, "um corpo por combinação de bandas seguidas");
        bodies.ShouldContain(body => body.Split('\n').Length == Bands, "o fã que segue as dez bandas não tem as dez linhas");
        bodies.Select(body => body.Split('\n').Length).Distinct().Order()
            .ShouldBe(Enumerable.Range(1, Bands), "os corpos não cobrem de 1 a 10 shows");
    }

    /// <summary>O conjunto de bandas (bits) seguido pelo fã <paramref name="index"/>: de 1 a 1.023.</summary>
    private static int Mask(int index) => (index % Combinations) + 1;
}
