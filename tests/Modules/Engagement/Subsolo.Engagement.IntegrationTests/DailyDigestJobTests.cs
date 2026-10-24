using System.Diagnostics;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0031 IT-04 (§5: resumo para 10.000 seguidores em ≤ 60 s) — no Subsolo.Api em memória com o PostgreSQL real e o
/// relógio fixo: dois shows novos cadastrados pela API na cidade de desenvolvimento e registrados em
/// <c>engagement.published_shows</c> há 1 hora; 10.000 fãs (usuários com e-mail) que seguem a cidade e têm o resumo diário
/// ligado; e linhas antigas em <c>published_shows</c> (8 e 10 dias, e uma de 6 dias). Disparado o job
/// <c>Engagement.DailyDigest</c>, as 10.000 entregas <c>digest</c> por e-mail são criadas em ≤ 60 s, cada aviso com os dois
/// shows, e as linhas com mais de 7 dias somem (a de 6 dias e as novas ficam). O job de entregas fica pausado (nenhum
/// e-mail sai; o envio é do SPEC-0030).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class DailyDigestJobTests(PostgresFixture postgres)
{
    private const int Fans = 10_000;

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    [Fact]
    [Trait("Spec", "SPEC-0031:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task TheDigestReachesTenThousandFansWithinOneMinuteAndOldRowsAreDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new RunningClock(DateTimeOffset.UtcNow);
        await using var api = new EngagementApiFactory(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        using var __ = admin;
        await QuartzJobs.PauseAsync(api, QuartzJobs.NoticeDeliveries, ct);
        var now = clock.GetUtcNow();
        var first = await admin.CreateShowAsync("Noite do Resumo I", now, days: 3, ct);
        var second = await admin.CreateShowAsync("Noite do Resumo II", now, days: 5, ct);
        var tenDaysOld = Guid.CreateVersion7();
        var eightDaysOld = Guid.CreateVersion7();
        var sixDaysOld = Guid.CreateVersion7();
        await NoticesDatabase.SeedPublishedShowsAsync(
            api.ConnectionString,
            [
                new PublishedShowRow(first.Id, EngagementApiFactory.City, [], now.AddHours(-1)),
                new PublishedShowRow(second.Id, EngagementApiFactory.City, [], now.AddHours(-1)),
                new PublishedShowRow(tenDaysOld, EngagementApiFactory.City, [], now.AddDays(-10)),
                new PublishedShowRow(eightDaysOld, EngagementApiFactory.City, [], now.AddDays(-8)),
                new PublishedShowRow(sixDaysOld, EngagementApiFactory.City, [], now.AddDays(-6)),
            ],
            ct);

        var fans = Enumerable.Range(0, Fans).Select(_ => Guid.CreateVersion7()).ToList();
        await NoticesDatabase.SeedUsersAsync(api.ConnectionString, fans, id => $"fa-{id:N}@subsolo.test", ct);
        await EngagementDatabase.SeedAsync(api.ConnectionString, fans, "city", EngagementApiFactory.City, ct);
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
        elapsed.ShouldBeLessThanOrEqualTo(Budget, $"o resumo para {Fans} fãs levou {elapsed.TotalSeconds:F1} s");
        var deliveries = (await NoticesDatabase.DeliveriesAsync(api.ConnectionString, ct)).Where(row => row.Kind == "digest").ToList();
        deliveries.Select(row => row.UserId).ShouldBeUnique("resumo repetido para o mesmo fã");
        deliveries.Select(row => row.UserId).ToHashSet().SetEquals(fans).ShouldBeTrue("as entregas não são exatamente dos 10.000 fãs");

        var notices = (await NoticesDatabase.NoticesAsync(api.ConnectionString, ct)).Where(row => row.Kind == "digest").ToList();
        notices.ShouldNotBeEmpty();
        notices.ShouldAllBe(notice => notice.Title == "Shows novos de quem você segue" && notice.Url == "/");
        string[] lines =
        [
            $"{first.Day} · Cova Rasa · {NoticeRequests.Venue}, {NoticeRequests.CityName}",
            $"{second.Day} · Cova Rasa · {NoticeRequests.Venue}, {NoticeRequests.CityName}",
        ];
        foreach (var notice in notices)
        {
            notice.Body.Split('\n').ShouldBe(lines, $"corpo do resumo fora do §6: {notice.Body}");
        }

        var remaining = (await NoticesDatabase.PublishedShowsAsync(api.ConnectionString, ct)).Select(row => row.ShowId).ToList();
        remaining.ShouldNotContain(tenDaysOld, "a linha de 10 dias não foi apagada");
        remaining.ShouldNotContain(eightDaysOld, "a linha de 8 dias não foi apagada");
        remaining.ShouldContain(sixDaysOld, "a linha de 6 dias foi apagada");
        remaining.ShouldContain(first.Id);
        remaining.ShouldContain(second.Id);
    }
}
