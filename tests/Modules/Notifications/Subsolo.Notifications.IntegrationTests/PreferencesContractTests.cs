using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Notifications.IntegrationTests.Infrastructure;

namespace Subsolo.Notifications.IntegrationTests;

/// <summary>
/// SPEC-0031 — itens herdados da revisão da SPEC-0030 sobre as preferências, no Subsolo.Api em memória:
/// <list type="bullet">
/// <item>H30-03 (Emenda 1, contrato v2) — <c>PUT /api/v1/me/notification-preferences</c> com um dos quatro campos
/// ausente ou não booleano (texto, número, nulo) → 400 <c>notifications.invalid_preferences</c>, e nada muda;</item>
/// <item>H30-07 — o fã que já tem a linha de preferências, com o show novo desligado, e cujo aparelho o servidor perdeu
/// (410, nenhum aparelho) inscreve o aparelho de novo (o painel reenvia a inscrição ao abrir): o show novo continua
/// desligado e as outras escolhas não mudam; na primeira inscrição de quem não tem linha, o show novo liga.</item>
/// </list>
/// </summary>
[Collection(nameof(MailpitCollection))]
public sealed class PreferencesContractTests(MailpitServer mailpit, PostgresFixture postgres)
{
    private const string InvalidPreferences = "notifications.invalid_preferences";

    public static TheoryData<string, string> IncompleteBodies => new()
    {
        { "sem newShowPush", """{"dailyDigestEmail":true,"reminderPush":true,"reminderEmail":true}""" },
        { "sem dailyDigestEmail", """{"newShowPush":true,"reminderPush":true,"reminderEmail":true}""" },
        { "sem reminderPush", """{"newShowPush":true,"dailyDigestEmail":true,"reminderEmail":true}""" },
        { "sem reminderEmail", """{"newShowPush":true,"dailyDigestEmail":true,"reminderPush":true}""" },
        { "newShowPush texto", """{"newShowPush":"sim","dailyDigestEmail":true,"reminderPush":true,"reminderEmail":true}""" },
        { "dailyDigestEmail número", """{"newShowPush":true,"dailyDigestEmail":1,"reminderPush":true,"reminderEmail":true}""" },
        { "reminderEmail nulo", """{"newShowPush":true,"dailyDigestEmail":true,"reminderPush":true,"reminderEmail":null}""" },
        { "corpo vazio", "{}" },
    };

    [Theory]
    [Trait("Spec", "SPEC-0031:H30-03")]
    [Trait("Kind", "Integration")]
    [MemberData(nameof(IncompleteBodies))]
    public async Task AnIncompleteOrNonBooleanBodyIs400AndChangesNothing(string label, string json)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit);
        var (fan, _) = await api.SignedInAsync($"fa.prefs-{Guid.NewGuid():N}@subsolo.test", ct);
        using var _ = fan;
        using (var put = await fan.MutateAsync(
            HttpMethod.Put,
            NotificationsRequests.Preferences,
            NotificationsRequests.PreferencesBody(newShowPush: false, dailyDigestEmail: false, reminderPush: false, reminderEmail: false),
            ct))
        {
            await put.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        }

        using var response = await fan.SendRawJsonAsync(HttpMethod.Put, NotificationsRequests.Preferences, json, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, InvalidPreferences, ct);
        (await fan.PreferencesAsync(ct)).ShouldBePreferences(
            newShowPush: false, dailyDigestEmail: false, reminderPush: false, reminderEmail: false, pushDevices: 0);
        label.ShouldNotBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:H30-03")]
    [Trait("Kind", "Integration")]
    public async Task TheFourFieldsStillSave()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit);
        var (fan, _) = await api.SignedInAsync($"fa.prefs-{Guid.NewGuid():N}@subsolo.test", ct);
        using var _ = fan;

        using var put = await fan.MutateAsync(
            HttpMethod.Put,
            NotificationsRequests.Preferences,
            NotificationsRequests.PreferencesBody(newShowPush: true, dailyDigestEmail: true, reminderPush: false, reminderEmail: true),
            ct);

        (await put.ShouldBeJsonAsync(HttpStatusCode.OK, ct)).ShouldBePreferences(
            newShowPush: true, dailyDigestEmail: true, reminderPush: false, reminderEmail: true, pushDevices: 0);
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:H30-07")]
    [Trait("Kind", "Integration")]
    public async Task ResubscribingAfterTheServerLostTheDeviceKeepsNewShowOff()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit);
        var (fan, _) = await api.SignedInAsync($"fa.reinscreve-{Guid.NewGuid():N}@subsolo.test", ct);
        using var _ = fan;
        using var subscriber = new PushSubscriber();
        using (var put = await fan.MutateAsync(
            HttpMethod.Put,
            NotificationsRequests.Preferences,
            NotificationsRequests.PreferencesBody(newShowPush: false, dailyDigestEmail: true, reminderPush: true, reminderEmail: false),
            ct))
        {
            await put.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        }

        await fan.SubscribeAsync(NotificationsRequests.FcmEndpoint("h30-07"), subscriber, ct);

        (await fan.PreferencesAsync(ct)).ShouldBePreferences(
            newShowPush: false, dailyDigestEmail: true, reminderPush: true, reminderEmail: false, pushDevices: 1);
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:H30-07")]
    [Trait("Kind", "Integration")]
    public async Task TheFirstSubscriptionOfAFanWithoutPreferencesTurnsNewShowOn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit);
        var (fan, _) = await api.SignedInAsync($"fa.primeira-{Guid.NewGuid():N}@subsolo.test", ct);
        using var _ = fan;
        using var subscriber = new PushSubscriber();

        await fan.SubscribeAsync(NotificationsRequests.FcmEndpoint("h30-07"), subscriber, ct);

        (await fan.PreferencesAsync(ct)).ShouldBePreferences(
            newShowPush: true, dailyDigestEmail: false, reminderPush: true, reminderEmail: false, pushDevices: 1);
    }
}

/// <summary>SPEC-0031 — corpo JSON cru (campos ausentes ou de outro tipo), com o <c>X-CSRF</c>.</summary>
internal static class RawJsonRequests
{
    public static async Task<HttpResponseMessage> SendRawJsonAsync(
        this NotificationsApiClient client, HttpMethod method, string route, string json, CancellationToken ct)
    {
        var token = await client.CsrfTokenAsync(ct);
        return await client.SendAsync(method, route, ct, new RawJson(json), token);
    }
}

/// <summary>Um corpo já serializado (o <c>JsonContent</c> do cliente escreve o texto como está).</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(RawJsonConverter))]
internal sealed record RawJson(string Json);

internal sealed class RawJsonConverter : System.Text.Json.Serialization.JsonConverter<RawJson>
{
    public override RawJson Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options) =>
        throw new NotSupportedException();

    public override void Write(System.Text.Json.Utf8JsonWriter writer, RawJson value, System.Text.Json.JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteRawValue(value.Json);
    }
}
