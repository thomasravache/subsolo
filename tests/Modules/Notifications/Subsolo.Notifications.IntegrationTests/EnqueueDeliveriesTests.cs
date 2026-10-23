using System.Diagnostics;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Notifications.IntegrationTests.Infrastructure;

namespace Subsolo.Notifications.IntegrationTests;

/// <summary>
/// SPEC-0030 IT-03 (§5) — com 10.000 usuários com preferências variadas gravadas direto no schema (metade com o show novo
/// por push ligado; dois terços com um aparelho inscrito), <c>EnqueueAsync</c> de um aviso <c>new_show</c> para todos
/// grava, em ≤ 2 s, uma entrega <c>push</c> só para quem tem o show novo ligado e um aparelho (sem preferência ligada ou
/// sem aparelho, nada; <c>new_show</c> não vai por e-mail); chamado de novo com a mesma <c>DedupeKey</c>, não cria
/// nenhuma entrega a mais (idempotência por usuário e chave, ADR-0014). Os envios vão ao servidor de push falso.
/// </summary>
[Collection(nameof(MailpitCollection))]
public sealed class EnqueueDeliveriesTests(MailpitServer mailpit, PostgresFixture postgres)
{
    private const int Users = 10_000;

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    [Fact]
    [Trait("Spec", "SPEC-0030:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task TenThousandRecipientsGetOneDeliveryPerAllowedChannelOnceAndFast()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit);
        api.CreateApiClient().Dispose();
        using var subscriber = new PushSubscriber();
        var users = Enumerable.Range(0, Users).Select(i => (Index: i, Id: Guid.CreateVersion7())).ToList();
        await NotificationsDatabase.SeedPreferencesAsync(
            api.ConnectionString,
            users.Select(user => (user.Id, NewShowPush: user.Index % 2 == 0, DailyDigestEmail: user.Index % 5 == 0, ReminderPush: true, ReminderEmail: false)),
            ct);
        await NotificationsDatabase.SeedSubscriptionsAsync(
            api.ConnectionString,
            users.Where(user => user.Index % 3 != 0)
                .Select(user => (user.Id, $"https://fcm.googleapis.com/fcm/send/it03-{user.Index}", subscriber.P256dh, subscriber.Auth)),
            ct);
        var expected = users.Where(user => user.Index % 2 == 0 && user.Index % 3 != 0).Select(user => user.Id).ToHashSet();
        var notice = new Notice(
            "new_show", $"it03-{Guid.NewGuid():N}", "Show novo: Podridão", "Sábado no Porão do Rock", "https://localhost/cidade-exemplo");
        var ids = users.Select(user => user.Id).ToList();

        var watch = Stopwatch.StartNew();
        var first = await api.EnqueueAsync(ids, notice, ct);
        watch.Stop();

        first.IsSuccess.ShouldBeTrue(first.IsFailure ? $"{first.Error.Code} — {first.Error.Description}" : null);
        watch.Elapsed.ShouldBeLessThanOrEqualTo(Budget, $"EnqueueAsync de {Users} destinatários levou {watch.Elapsed.TotalMilliseconds:F0} ms");
        var deliveries = (await NotificationsDatabase.DeliveriesAsync(api.ConnectionString, ct))
            .Where(row => row.DedupeKey == notice.DedupeKey)
            .ToList();
        deliveries.ShouldAllBe(row => row.Kind == "new_show" && row.Channel == "push");
        deliveries.Select(row => row.UserId).ShouldBe(expected, ignoreOrder: true, "entregas fora de quem tem o show novo ligado e um aparelho");

        var second = await api.EnqueueAsync(ids, notice, ct);

        second.IsSuccess.ShouldBeTrue(second.IsFailure ? $"{second.Error.Code} — {second.Error.Description}" : null);
        (await NotificationsDatabase.DeliveriesAsync(api.ConnectionString, ct))
            .Count(row => row.DedupeKey == notice.DedupeKey)
            .ShouldBe(expected.Count, "a mesma DedupeKey gerou entregas de novo");
    }
}
