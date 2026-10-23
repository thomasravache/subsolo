using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Notifications.IntegrationEvents;
using Subsolo.Notifications.IntegrationTests.Infrastructure;

namespace Subsolo.Notifications.IntegrationTests;

/// <summary>
/// SPEC-0030 IT-04 (§5, ADR-0014) — com o servidor de push falso registrando as chamadas e 1.000 aparelhos inscritos
/// (gravados direto no schema, com o show novo ligado), um aviso <c>new_show</c> enfileirado é entregue a todos em ≤ 60 s:
/// cada chamada é um <c>POST</c> no endpoint da inscrição, com <c>Content-Encoding: aes128gcm</c>, <c>TTL</c> e
/// <c>Authorization: vapid t=…, k=…</c> válido (JWT ES256 da chave VAPID, <c>aud</c> = origem do serviço, <c>exp</c> em até
/// 24 h, <c>sub</c> configurado); o corpo, decifrado com as chaves do navegador, é o JSON <c>{title, body, url}</c> do
/// aviso, sem dado pessoal. A inscrição que responde 410 é removida; a que responde 500 é tentada de novo (e não é
/// removida); nenhum aparelho recebe o aviso duas vezes.
/// </summary>
[Collection(nameof(MailpitCollection))]
public sealed class WebPushDeliveryTests(MailpitServer mailpit, PostgresFixture postgres)
{
    private const int Devices = 1_000;

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);

    [Fact]
    [Trait("Spec", "SPEC-0030:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task AThousandPushesAreEncryptedSignedAndDeliveredDeadOnesRemovedFailedOnesRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new NotificationsApiFactory(
            await NotificationsApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), mailpit);
        api.CreateApiClient().Dispose();
        PushSubscriber[] browsers = [new(), new(), new(), new()];
        try
        {
            var devices = Enumerable.Range(0, Devices)
                .Select(i => new Device(Guid.CreateVersion7(), $"https://fcm.googleapis.com/fcm/send/it04-{i}-{Guid.NewGuid():N}", browsers[i % browsers.Length]))
                .ToList();
            var dead = devices[0];
            var failing = devices[1];
            var healthy = devices.Skip(2).ToList();
            api.Push.RespondWith(dead.Endpoint, HttpStatusCode.Gone);
            api.Push.RespondWith(failing.Endpoint, HttpStatusCode.InternalServerError);
            await NotificationsDatabase.SeedPreferencesAsync(
                api.ConnectionString,
                devices.Select(device => (device.UserId, NewShowPush: true, DailyDigestEmail: false, ReminderPush: true, ReminderEmail: false)),
                ct);
            await NotificationsDatabase.SeedSubscriptionsAsync(
                api.ConnectionString,
                devices.Select(device => (device.UserId, device.Endpoint, device.Browser.P256dh, device.Browser.Auth)),
                ct);
            var notice = new Notice(
                "new_show",
                $"it04-{Guid.NewGuid():N}",
                "Show novo: Podridão",
                "Sábado, 21h, no Porão do Rock",
                "https://localhost/cidade-exemplo/shows/podridao-no-porao");

            var watch = Stopwatch.StartNew();
            var enqueued = await api.EnqueueAsync([.. devices.Select(device => device.UserId)], notice, ct);
            enqueued.IsSuccess.ShouldBeTrue(enqueued.IsFailure ? $"{enqueued.Error.Code} — {enqueued.Error.Description}" : null);

            var healthyEndpoints = healthy.Select(device => device.Endpoint).ToHashSet(StringComparer.Ordinal);
            var delivered = await Timing.EventuallyAsync(
                () => Task.FromResult(
                    api.Push.Calls.Where(call => call.Status == HttpStatusCode.Created)
                        .Select(call => call.Endpoint).Distinct(StringComparer.Ordinal).Count() == healthyEndpoints.Count),
                Budget,
                ct);
            watch.Stop();
            delivered.ShouldBeTrue(
                $"em {Budget.TotalSeconds:F0} s, só {api.Push.Calls.Count(call => call.Status == HttpStatusCode.Created)} de {healthyEndpoints.Count} pushes entregues");
            watch.Elapsed.ShouldBeLessThanOrEqualTo(Budget);

            (await Timing.EventuallyAsync(
                    async () => !(await NotificationsDatabase.SubscriptionsAsync(api.ConnectionString, ct)).Any(row => row.Endpoint == dead.Endpoint),
                    Budget,
                    ct))
                .ShouldBeTrue("a inscrição que respondeu 410 não foi removida");
            api.Push.CallsTo(dead.Endpoint).ShouldNotBeEmpty("a inscrição morta nem foi tentada");

            (await Timing.EventuallyAsync(() => Task.FromResult(api.Push.CallsTo(failing.Endpoint).Count >= 2), Budget, ct))
                .ShouldBeTrue($"a inscrição que respondeu 500 não foi tentada de novo ({api.Push.CallsTo(failing.Endpoint).Count} chamada(s))");
            (await NotificationsDatabase.SubscriptionsAsync(api.ConnectionString, ct))
                .ShouldContain(row => row.Endpoint == failing.Endpoint, "a inscrição com falha temporária (500) foi removida");

            var byEndpoint = devices.ToDictionary(device => device.Endpoint, StringComparer.Ordinal);
            var problems = new List<string>();
            foreach (var call in api.Push.Calls)
            {
                problems.AddRange(Check(call, byEndpoint, api, notice).Select(problem => $"{call.Endpoint}: {problem}"));
            }

            problems.Take(20).ShouldBeEmpty($"{problems.Count} problema(s) nas chamadas ao serviço de push");
            api.Push.Calls.Where(call => call.Status == HttpStatusCode.Created)
                .GroupBy(call => call.Endpoint, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ShouldBeEmpty("aparelho recebeu o mesmo aviso mais de uma vez");
        }
        finally
        {
            foreach (var browser in browsers)
            {
                browser.Dispose();
            }
        }
    }

    private static IEnumerable<string> Check(PushCall call, Dictionary<string, Device> byEndpoint, NotificationsApiFactory api, Notice notice)
    {
        if (!byEndpoint.TryGetValue(call.Endpoint, out var device))
        {
            yield return "chamada a um endpoint que não é de nenhuma inscrição";
            yield break;
        }

        if (call.Method != "POST")
        {
            yield return $"método {call.Method}";
        }

        if (!string.Equals(call.Header("Content-Encoding"), "aes128gcm", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"Content-Encoding = {call.Header("Content-Encoding")}";
        }

        if (call.Header("TTL") is null)
        {
            yield return "sem TTL";
        }

        foreach (var problem in VapidAuthorization.Problems(
            call.Header("Authorization"), api.Vapid, new Uri(call.Endpoint), NotificationsApiFactory.VapidSubject, call.At))
        {
            yield return problem;
        }

        string? text = null;
        string? failure = null;
        try
        {
            text = System.Text.Encoding.UTF8.GetString(device.Browser.Decrypt(call.Body));
        }
        catch (Exception exception) when (exception is InvalidDataException or System.Security.Cryptography.CryptographicException or ArgumentException)
        {
            failure = $"o corpo não decifra com as chaves da inscrição (aes128gcm): {exception.Message}";
        }

        if (failure is not null)
        {
            yield return failure;
            yield break;
        }

        using var payload = JsonDocument.Parse(text!);
        var root = payload.RootElement;
        if (!root.TryGetProperty("title", out var title) || title.GetString() != notice.Title
            || !root.TryGetProperty("body", out var body) || body.GetString() != notice.Body
            || !root.TryGetProperty("url", out var url) || url.GetString() != notice.Url)
        {
            yield return $"payload sem {{title, body, url}} do aviso: {text}";
        }

        if (text!.Contains(device.UserId.ToString(), StringComparison.OrdinalIgnoreCase) || text.Contains('@', StringComparison.Ordinal))
        {
            yield return $"payload com dado pessoal além do aviso: {text}";
        }
    }

    private sealed record Device(Guid UserId, string Endpoint, PushSubscriber Browser);
}
