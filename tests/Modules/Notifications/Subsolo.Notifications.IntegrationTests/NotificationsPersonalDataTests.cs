using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Notifications.IntegrationTests.Infrastructure;

namespace Subsolo.Notifications.IntegrationTests;

/// <summary>
/// SPEC-0030 IT-06 (ADR-0008) — um fã com inscrição de push, preferências e uma entrega (e outro fã com as dele):
/// <c>GET /api/v1/me/export</c> (Users, SPEC-0019) traz a seção <c>notifications</c> (por <c>INotificationsApi.ExportAsync</c>)
/// sem as chaves de criptografia da inscrição (<c>p256dh</c>, <c>auth</c>) e sem nada do outro fã; depois que o fã exclui a
/// conta (<c>DELETE /api/v1/me</c>, o Users chama <c>ForgetUserAsync</c>), tudo dele some do schema <c>notifications</c>
/// (inscrições, preferências e entregas), e o do outro fã fica.
/// </summary>
[Collection(nameof(MailpitCollection))]
public sealed class NotificationsPersonalDataTests(MailpitServer mailpit, PostgresFixture postgres)
{
    private const string FanEmail = "fa.exporta.avisos@subsolo.test";

    private static readonly TimeSpan DeletionTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Spec", "SPEC-0030:IT-06")]
    [Trait("Kind", "Integration")]
    public async Task TheExportHasTheNotificationsSectionWithoutKeysAndDeletingTheAccountForgetsEverything()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit);
        var (fan, fanId) = await api.SignedInAsync(FanEmail, ct);
        var (other, otherId) = await api.SignedInAsync("outro.fa.avisos@subsolo.test", ct);
        using var _ = fan;
        using var __ = other;
        using var fanBrowser = new PushSubscriber();
        using var otherBrowser = new PushSubscriber();
        var otherEndpoint = NotificationsRequests.FcmEndpoint("it06-outro");
        await fan.SubscribeAsync(NotificationsRequests.FcmEndpoint("it06"), fanBrowser, ct);
        await other.SubscribeAsync(otherEndpoint, otherBrowser, ct);
        using (var put = await fan.MutateAsync(
            HttpMethod.Put,
            NotificationsRequests.Preferences,
            NotificationsRequests.PreferencesBody(newShowPush: true, dailyDigestEmail: true, reminderPush: true, reminderEmail: false),
            ct))
        {
            await put.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        }

        var notice = new Notice("new_show", $"it06-{Guid.NewGuid():N}", "Show novo", "Sábado no Porão", "https://localhost/cidade-exemplo");
        (await api.EnqueueAsync([fanId, otherId], notice, ct)).IsSuccess.ShouldBeTrue();
        var fanRows = await NotificationsDatabase.RowsOfAsync(api.ConnectionString, fanId, ct);
        fanRows.ShouldBe((1L, 1L, 1L), "o fã devia ter uma inscrição, as preferências e uma entrega antes da exclusão");

        using (var response = await fan.GetAsync(NotificationsRequests.Export, ct))
        {
            var export = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
            export.GetProperty("sections").TryGetProperty("notifications", out var section)
                .ShouldBeTrue($"o export não tem a seção notifications: {export.GetProperty("sections")}");
            var text = section.ToString();
            text.ShouldNotContain(fanBrowser.P256dh, Case.Sensitive, "a seção traz a chave p256dh da inscrição");
            text.ShouldNotContain(fanBrowser.Auth, Case.Sensitive, "a seção traz o segredo auth da inscrição");
            text.ShouldNotContain(otherEndpoint, Case.Sensitive, "a seção traz a inscrição de outro fã");
            text.ShouldNotContain(otherBrowser.P256dh, Case.Sensitive, "a seção traz dado de outro fã");
            text.ShouldContain("true", Case.Sensitive, $"a seção não traz as preferências do fã: {text}");
        }

        using (var delete = await fan.MutateAsync(HttpMethod.Delete, NotificationsRequests.Me, new { confirmEmail = FanEmail }, ct))
        {
            delete.StatusCode.ShouldBe(HttpStatusCode.NoContent, NotificationsApiClient.Excerpt(await delete.Content.ReadAsStringAsync(ct)));
        }

        (await Timing.EventuallyAsync(
                async () => await NotificationsDatabase.RowsOfAsync(api.ConnectionString, fanId, ct) == (0L, 0L, 0L),
                DeletionTimeout,
                ct))
            .ShouldBeTrue($"o schema notifications ainda tem dados do usuário excluído: {await NotificationsDatabase.RowsOfAsync(api.ConnectionString, fanId, ct)}");
        var otherRows = await NotificationsDatabase.RowsOfAsync(api.ConnectionString, otherId, ct);
        otherRows.Subscriptions.ShouldBe(1L, "a inscrição de outro fã foi apagada");
        otherRows.Deliveries.ShouldBe(1L, "a entrega de outro fã foi apagada");
    }
}
