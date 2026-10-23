using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Notifications.IntegrationTests.Infrastructure;

namespace Subsolo.Notifications.IntegrationTests;

/// <summary>
/// SPEC-0030 IT-01 — com a API e o PostgreSQL reais: <c>GET /api/v1/push/public-key</c> é público e devolve a chave
/// VAPID configurada; o fã registra a mesma inscrição válida duas vezes (204, 204 — um registro só em
/// <c>notifications.push_subscriptions</c>, com as chaves); inscrições inválidas (endpoint <c>http</c>, domínio
/// desconhecido, chaves ausentes) dão 400 <c>notifications.invalid_subscription</c> sem gravar nada; <c>DELETE</c> pelo
/// endpoint remove; sem sessão é 401 e sem <c>X-CSRF</c> é 400 <c>security.csrf_invalid</c> (§5).
/// </summary>
[Collection(nameof(MailpitCollection))]
public sealed class PushSubscriptionApiTests(MailpitServer mailpit, PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0030:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task ThePublicKeyIsPublicAndIsTheConfiguredVapidKey()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await StartAsync(ct);
        using var anonymous = api.CreateApiClient();

        using var response = await anonymous.GetAsync(NotificationsRequests.PublicKey, ct);

        var body = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(["publicKey"], $"corpo fora do §6: {body}");
        body.GetProperty("publicKey").GetString().ShouldBe(api.Vapid.PublicKey);
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task TheSameValidSubscriptionTwiceIsStoredOnceAndDeleteRemovesIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await StartAsync(ct);
        var (fan, fanId) = await api.SignedInAsync("fa.inscreve@subsolo.test", ct);
        using var _ = fan;
        using var subscriber = new PushSubscriber();
        var endpoint = NotificationsRequests.FcmEndpoint("it01");

        await fan.SubscribeAsync(endpoint, subscriber, ct);
        await fan.SubscribeAsync(endpoint, subscriber, ct);

        var stored = (await NotificationsDatabase.SubscriptionsAsync(api.ConnectionString, ct))
            .Where(row => row.Endpoint == endpoint)
            .ToList()
            .ShouldHaveSingleItem("a mesma inscrição duas vezes gerou mais de um registro (ou nenhum)");
        stored.UserId.ShouldBe(fanId);
        stored.P256dh.ShouldBe(subscriber.P256dh);
        stored.Auth.ShouldBe(subscriber.Auth);

        using (var delete = await fan.MutateAsync(HttpMethod.Delete, NotificationsRequests.Subscriptions, new { endpoint }, ct))
        {
            delete.StatusCode.ShouldBe(
                HttpStatusCode.NoContent, NotificationsApiClient.Excerpt(await delete.Content.ReadAsStringAsync(ct)));
        }

        (await NotificationsDatabase.SubscriptionsAsync(api.ConnectionString, ct))
            .ShouldNotContain(row => row.Endpoint == endpoint, "DELETE não removeu a inscrição");
    }

    public static TheoryData<string, string?, string?> InvalidSubscriptions => new()
    {
        { "http://fcm.googleapis.com/fcm/send/it01-http", Keys.P256dh, Keys.Auth },
        { "https://evil.com/fcm/send/it01-desconhecido", Keys.P256dh, Keys.Auth },
        { "https://fcm.googleapis.com.evil.com/fcm/send/it01-sufixo", Keys.P256dh, Keys.Auth },
        { "https://fcm.googleapis.com/fcm/send/it01-sem-p256dh", null, Keys.Auth },
        { "https://fcm.googleapis.com/fcm/send/it01-sem-auth", Keys.P256dh, null },
        { "https://fcm.googleapis.com/fcm/send/it01-chaves-vazias", string.Empty, string.Empty },
    };

    [Theory]
    [Trait("Spec", "SPEC-0030:IT-01")]
    [Trait("Kind", "Integration")]
    [MemberData(nameof(InvalidSubscriptions))]
    public async Task AnInvalidSubscriptionIsRejectedWithoutStoringAnything(string endpoint, string? p256dh, string? auth)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await StartAsync(ct);
        var (fan, _) = await api.SignedInAsync("fa.inscricao.invalida@subsolo.test", ct);
        using var __ = fan;

        using var response = await fan.PostAsync(
            NotificationsRequests.Subscriptions, NotificationsRequests.Subscription(endpoint, p256dh, auth), ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "notifications.invalid_subscription", ct);
        (await NotificationsDatabase.SubscriptionsAsync(api.ConnectionString, ct)).ShouldBeEmpty("a inscrição inválida foi gravada");
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task WithoutSessionIs401AndWithoutCsrfIs400()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await StartAsync(ct);
        using var anonymous = api.CreateApiClient();
        var (fan, _) = await api.SignedInAsync("fa.sem.csrf@subsolo.test", ct);
        using var _ = fan;
        using var subscriber = new PushSubscriber();
        var body = NotificationsRequests.Subscription(NotificationsRequests.FcmEndpoint("it01-acesso"), subscriber.P256dh, subscriber.Auth);

        using (var withoutSession = await anonymous.PostAsync(NotificationsRequests.Subscriptions, body, ct))
        {
            await withoutSession.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
        }

        using (var withoutCsrf = await fan.SendAsync(HttpMethod.Post, NotificationsRequests.Subscriptions, ct, body))
        {
            await withoutCsrf.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
        }

        (await NotificationsDatabase.SubscriptionsAsync(api.ConnectionString, ct)).ShouldBeEmpty();
    }

    private async Task<NotificationsApiFactory> StartAsync(CancellationToken ct)
    {
        var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit);
        api.CreateApiClient().Dispose();
        return api;
    }

    /// <summary>Chaves válidas de um navegador de teste (só para as linhas em que o endpoint é o problema).</summary>
    private static class Keys
    {
        private static readonly PushSubscriber Subscriber = new();

        public static string P256dh => Subscriber.P256dh;

        public static string Auth => Subscriber.Auth;
    }
}
