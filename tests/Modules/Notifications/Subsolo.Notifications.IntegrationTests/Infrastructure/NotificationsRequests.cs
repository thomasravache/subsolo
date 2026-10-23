using System.Net;
using System.Text.Json;

namespace Subsolo.Notifications.IntegrationTests.Infrastructure;

/// <summary>SPEC-0030 — as rotas do §6 e os corpos usados pelos testes.</summary>
internal static class NotificationsRequests
{
    public const string PublicKey = "/api/v1/push/public-key";

    public const string Subscriptions = "/api/v1/me/push-subscriptions";

    public const string Preferences = "/api/v1/me/notification-preferences";

    public const string Unsubscribe = "/api/v1/notifications/unsubscribe";

    /// <summary>SPEC-0019 — export e exclusão da conta (Users).</summary>
    public const string Export = "/api/v1/me/export";

    public const string Me = "/api/v1/me";

    /// <summary>Endpoint de inscrição do FCM (serviço conhecido), único por chamada.</summary>
    public static string FcmEndpoint(string label) => $"https://fcm.googleapis.com/fcm/send/{label}-{Guid.NewGuid():N}";

    /// <summary>Corpo de <c>POST /me/push-subscriptions</c>.</summary>
    public static object Subscription(string endpoint, string? p256dh, string? auth) =>
        new { endpoint, keys = new { p256dh, auth } };

    /// <summary>Corpo de <c>PUT /me/notification-preferences</c>.</summary>
    public static object PreferencesBody(bool newShowPush, bool dailyDigestEmail, bool reminderPush, bool reminderEmail) =>
        new { newShowPush, dailyDigestEmail, reminderPush, reminderEmail };

    /// <summary><c>POST /me/push-subscriptions</c> com 204.</summary>
    public static async Task SubscribeAsync(this NotificationsApiClient client, string endpoint, PushSubscriber subscriber, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        using var response = await client.PostAsync(Subscriptions, Subscription(endpoint, subscriber.P256dh, subscriber.Auth), ct);
        response.StatusCode.ShouldBe(
            HttpStatusCode.NoContent,
            $"POST {Subscriptions}: {NotificationsApiClient.Excerpt(await response.Content.ReadAsStringAsync(ct))}");
    }

    /// <summary><c>GET /me/notification-preferences</c> com 200 e <c>no-store</c>; devolve o corpo.</summary>
    public static async Task<JsonElement> PreferencesAsync(this NotificationsApiClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(Preferences, ct);
        response.ShouldBeNoStore();
        return await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
    }

    /// <summary>O corpo de preferências tem exatamente os campos e valores do §6.</summary>
    public static void ShouldBePreferences(
        this JsonElement body,
        bool newShowPush,
        bool dailyDigestEmail,
        bool reminderPush,
        bool reminderEmail,
        int pushDevices)
    {
        body.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["newShowPush", "dailyDigestEmail", "reminderPush", "reminderEmail", "pushDevices"],
            ignoreOrder: true,
            $"corpo fora do §6: {body}");
        body.GetProperty("newShowPush").GetBoolean().ShouldBe(newShowPush, $"newShowPush: {body}");
        body.GetProperty("dailyDigestEmail").GetBoolean().ShouldBe(dailyDigestEmail, $"dailyDigestEmail: {body}");
        body.GetProperty("reminderPush").GetBoolean().ShouldBe(reminderPush, $"reminderPush: {body}");
        body.GetProperty("reminderEmail").GetBoolean().ShouldBe(reminderEmail, $"reminderEmail: {body}");
        body.GetProperty("pushDevices").GetInt32().ShouldBe(pushDevices, $"pushDevices: {body}");
    }
}
