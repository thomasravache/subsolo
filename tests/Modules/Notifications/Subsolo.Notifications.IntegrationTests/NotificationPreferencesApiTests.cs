using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Notifications.IntegrationTests.Infrastructure;

namespace Subsolo.Notifications.IntegrationTests;

/// <summary>
/// SPEC-0030 IT-02 — <c>GET /api/v1/me/notification-preferences</c> (<c>no-store</c>) de um fã novo devolve os padrões
/// do §2: show novo desligado até ativar o push, resumo diário e lembrete por e-mail desligados (opt-in, ADR-0008),
/// lembrete por push ligado, nenhum aparelho; depois de ativar o push (primeira inscrição), o show novo liga e
/// <c>pushDevices</c> = 1; o <c>PUT</c> com os quatro booleanos devolve 200 com o mesmo corpo e a mudança persiste. Sem
/// sessão é 401; o <c>PUT</c> sem <c>X-CSRF</c> é 400 e não muda nada.
/// </summary>
[Collection(nameof(MailpitCollection))]
public sealed class NotificationPreferencesApiTests(MailpitServer mailpit, PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0030:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task DefaultsThenActivatingPushThenChangesArePersisted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit);
        var (fan, _) = await api.SignedInAsync("fa.preferencias@subsolo.test", ct);
        using var _ = fan;
        using var subscriber = new PushSubscriber();

        (await fan.PreferencesAsync(ct)).ShouldBePreferences(
            newShowPush: false, dailyDigestEmail: false, reminderPush: true, reminderEmail: false, pushDevices: 0);

        await fan.SubscribeAsync(NotificationsRequests.FcmEndpoint("it02"), subscriber, ct);

        (await fan.PreferencesAsync(ct)).ShouldBePreferences(
            newShowPush: true, dailyDigestEmail: false, reminderPush: true, reminderEmail: false, pushDevices: 1);

        using (var put = await fan.MutateAsync(
            HttpMethod.Put,
            NotificationsRequests.Preferences,
            NotificationsRequests.PreferencesBody(newShowPush: false, dailyDigestEmail: true, reminderPush: false, reminderEmail: true),
            ct))
        {
            (await put.ShouldBeJsonAsync(HttpStatusCode.OK, ct)).ShouldBePreferences(
                newShowPush: false, dailyDigestEmail: true, reminderPush: false, reminderEmail: true, pushDevices: 1);
        }

        (await fan.PreferencesAsync(ct)).ShouldBePreferences(
            newShowPush: false, dailyDigestEmail: true, reminderPush: false, reminderEmail: true, pushDevices: 1);
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task WithoutSessionIs401AndPutWithoutCsrfChangesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit);
        using var anonymous = api.CreateApiClient();
        var (fan, _) = await api.SignedInAsync("fa.preferencias.csrf@subsolo.test", ct);
        using var _ = fan;
        var body = NotificationsRequests.PreferencesBody(newShowPush: true, dailyDigestEmail: true, reminderPush: true, reminderEmail: true);

        using (var get = await anonymous.GetAsync(NotificationsRequests.Preferences, ct))
        {
            await get.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
        }

        using (var withoutCsrf = await fan.SendAsync(HttpMethod.Put, NotificationsRequests.Preferences, ct, body))
        {
            await withoutCsrf.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
        }

        (await fan.PreferencesAsync(ct)).ShouldBePreferences(
            newShowPush: false, dailyDigestEmail: false, reminderPush: true, reminderEmail: false, pushDevices: 0);
    }
}
