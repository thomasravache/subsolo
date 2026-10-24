using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0031 — o consumidor de <c>ShowPublished</c> do Engagement, pela inbox (SPEC-0007), no Subsolo.Api em memória com
/// o PostgreSQL real e o relógio fixo; três fãs seguem a cidade de desenvolvimento, com aparelho e o show novo ligado:
/// <list type="bullet">
/// <item>IT-02 — o mesmo <c>ShowPublished</c> entregue de novo à inbox (mesmo conteúdo, mensagem nova) é processado sem
/// erro e não cria entregas a mais nem uma segunda linha em <c>published_shows</c>;</item>
/// <item>IT-03 — o evento de um show cancelado antes do processamento, e o de um show que já começou quando o evento é
/// processado (o relógio passa do início com a inbox parada), não criam nenhuma entrega.</item>
/// </list>
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class NewShowInboxTests(PostgresFixture postgres)
{
    private const string ShowPublished = "ShowPublishedIntegrationEvent";

    private const int Fans = 3;

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Spec", "SPEC-0031:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task TheSameShowPublishedDeliveredTwiceCreatesNoDuplicateDeliveries()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new RunningClock(DateTimeOffset.UtcNow);
        await using var api = new EngagementApiFactory(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        using var __ = admin;
        await SeedFansAsync(api, ct);
        await QuartzJobs.PauseAsync(api, QuartzJobs.NoticeDeliveries, ct);

        var show = await admin.CreateShowAsync("Noite Repetida", clock.GetUtcNow(), days: 10, ct);
        var key = $"new_show:{show.Id}";
        (await Timing.EventuallyAsync(async () => await DeliveriesOfAsync(api, key, ct) == Fans, Wait, ct))
            .ShouldBeTrue($"o show novo não virou {Fans} entregas (há {await DeliveriesOfAsync(api, key, ct)})");
        var first = (await NoticesDatabase.InboxAsync(api.ConnectionString, ShowPublished, ct))
            .ShouldHaveSingleItem("o ShowPublished não está (uma vez) na inbox do Engagement");

        var copy = await NoticesDatabase.RedeliverAsync(api.ConnectionString, first.Id, ct);
        (await Timing.EventuallyAsync(
                async () => (await NoticesDatabase.InboxAsync(api.ConnectionString, ShowPublished, ct))
                    .Any(row => row.Id == copy && (row.ProcessedOnUtc is not null || row.Error is not null)),
                Wait,
                ct))
            .ShouldBeTrue("o evento entregue de novo não foi processado pela inbox");

        var redelivered = (await NoticesDatabase.InboxAsync(api.ConnectionString, ShowPublished, ct)).Single(row => row.Id == copy);
        redelivered.Error.ShouldBeNull("o consumidor falhou com o evento repetido");
        redelivered.ProcessedOnUtc.ShouldNotBeNull();
        var deliveries = (await NoticesDatabase.DeliveriesAsync(api.ConnectionString, ct)).Where(row => row.DedupeKey == key).ToList();
        deliveries.Count.ShouldBe(Fans, "o evento repetido criou entregas a mais");
        deliveries.Select(row => (row.UserId, row.Channel)).ShouldBeUnique();
        (await NoticesDatabase.PublishedShowsAsync(api.ConnectionString, ct)).Count(row => row.ShowId == show.Id)
            .ShouldBe(1, "o show não tem exatamente uma linha em published_shows");
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task AShowCancelledBeforeTheEventIsProcessedNotifiesNobody()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new RunningClock(DateTimeOffset.UtcNow);
        await using var api = new EngagementApiFactory(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        using var __ = admin;
        await SeedFansAsync(api, ct);
        await QuartzJobs.PauseAsync(api, QuartzJobs.EngagementInbox, ct);

        var show = await admin.CreateShowAsync("Noite Cancelada", clock.GetUtcNow(), days: 10, ct);
        await WaitForEventInInboxAsync(api, ct);
        await admin.CancelShowAsync(show.Id, ct);
        await QuartzJobs.ResumeAsync(api, QuartzJobs.EngagementInbox, ct);
        await WaitForEventProcessedAsync(api, ct);

        (await DeliveriesOfAsync(api, $"new_show:{show.Id}", ct)).ShouldBe(0, "o show cancelado virou aviso");
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task AShowThatAlreadyStartedWhenTheEventIsProcessedNotifiesNobody()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new RunningClock(DateTimeOffset.UtcNow);
        await using var api = new EngagementApiFactory(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        using var __ = admin;
        await SeedFansAsync(api, ct);
        await QuartzJobs.PauseAsync(api, QuartzJobs.EngagementInbox, ct);

        var show = await admin.CreateShowAsync("Noite que Começou", clock.GetUtcNow(), days: 1, ct);
        await WaitForEventInInboxAsync(api, ct);
        clock.SetUtcNow(NoticeRequests.UtcOf(show.StartsLocal).AddHours(1));
        await QuartzJobs.ResumeAsync(api, QuartzJobs.EngagementInbox, ct);
        await WaitForEventProcessedAsync(api, ct);

        (await DeliveriesOfAsync(api, $"new_show:{show.Id}", ct)).ShouldBe(0, "o show já começado virou aviso");
    }

    /// <summary><see cref="Fans"/> fãs da cidade de desenvolvimento com aparelho e o show novo ligado.</summary>
    private static async Task SeedFansAsync(EngagementApiFactory api, CancellationToken ct)
    {
        var fans = Enumerable.Range(0, Fans).Select(_ => Guid.CreateVersion7()).ToList();
        await EngagementDatabase.SeedAsync(api.ConnectionString, fans, "city", EngagementApiFactory.City, ct);
        await NoticesDatabase.SeedPreferencesAsync(api.ConnectionString, fans.Select(id => (id, NewShowPush: true, DailyDigestEmail: false)), ct);
        await NoticesDatabase.SeedSubscriptionsAsync(api.ConnectionString, fans, ct);
    }

    private static async Task WaitForEventInInboxAsync(EngagementApiFactory api, CancellationToken ct) =>
        (await Timing.EventuallyAsync(
                async () => (await NoticesDatabase.InboxAsync(api.ConnectionString, ShowPublished, ct)).Count > 0,
                Wait,
                ct))
            .ShouldBeTrue("o ShowPublished não chegou à inbox do Engagement (o Engagement não consome o evento)");

    private static async Task WaitForEventProcessedAsync(EngagementApiFactory api, CancellationToken ct)
    {
        (await Timing.EventuallyAsync(
                async () => (await NoticesDatabase.InboxAsync(api.ConnectionString, ShowPublished, ct))
                    .All(row => row.ProcessedOnUtc is not null || row.Error is not null),
                Wait,
                ct))
            .ShouldBeTrue("a inbox do Engagement não processou o ShowPublished");
        (await NoticesDatabase.InboxAsync(api.ConnectionString, ShowPublished, ct))
            .ShouldAllBe(row => row.Error == null, "o consumidor falhou com o ShowPublished");
    }

    private static async Task<int> DeliveriesOfAsync(EngagementApiFactory api, string key, CancellationToken ct) =>
        (await NoticesDatabase.DeliveriesAsync(api.ConnectionString, ct)).Count(row => row.DedupeKey == key);
}
