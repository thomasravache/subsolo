using System.Globalization;
using System.Net;
using System.Text.Json;
using Subsolo.IntegrationTests.Common;
using Subsolo.Moderation.IntegrationTests.Infrastructure;

namespace Subsolo.Moderation.IntegrationTests;

/// <summary>
/// SPEC-0033 IT-04 — com 500 shows denunciados (50 fãs, 10 denúncias cada, pela API), a fila do admin tem os 500 itens
/// com as chaves do §6, na ordem da primeira denúncia (mais antigos primeiro), sem identificar quem denunciou (nem id nem
/// e-mail), e responde em p95 ≤ 300 ms (20 chamadas medidas, depois de uma de aquecimento).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class QueuePerformanceTests(PostgresFixture postgres)
{
    private const int Shows = 500;
    private const int Fans = 50;
    private const int ReportsPerFan = Shows / Fans;
    private const int QueueCalls = 20;

    private static readonly TimeSpan QueueBudget = TimeSpan.FromMilliseconds(300);

    [Fact]
    [Trait("Spec", "SPEC-0033:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task TheQueueWith500ReportedShowsHasNoAuthorsAndAnswersUnder300MillisecondsAtP95()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new ModerationApiFactory(await ModerationApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(ModerationApiFactory.AdminEmail, ct);
        using var _ = admin;
        var template = await admin.CreateShowAsync("Noite em Massa", ct);
        var shows = new List<Guid> { template.Id };
        shows.AddRange(await ModerationDatabase.CloneShowAsync(api.ConnectionString, template.Id, Shows - 1, ct));
        var reasons = new[] { "fake", "cancelled_unannounced", "wrong_info", "other" };
        var authors = new List<Guid>();
        for (var f = 0; f < Fans; f++)
        {
            var (fan, fanId) = await api.SignedInAsync(string.Create(CultureInfo.InvariantCulture, $"fila-{f:00}@subsolo.test"), ct);
            authors.Add(fanId);
            using (fan)
            {
                for (var r = 0; r < ReportsPerFan; r++)
                {
                    var index = (f * ReportsPerFan) + r;
                    await fan.ReportAsync(
                        shows[index],
                        reasons[index % reasons.Length],
                        ct,
                        string.Create(CultureInfo.InvariantCulture, $"Detalhe da denúncia {index:000}."));
                }
            }
        }

        var durations = new List<TimeSpan>();
        JsonElement queue = default;
        for (var i = 0; i <= QueueCalls; i++)
        {
            var (response, elapsed) = await Timing.TimedAsync(() => admin.GetAsync(ModerationRequests.AdminReports, ct));
            using (response)
            {
                queue = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
            }

            if (i > 0)
            {
                durations.Add(elapsed);
            }
        }

        Timing.Percentile(durations, 95).ShouldBeLessThanOrEqualTo(
            QueueBudget, $"p95 da fila com {Shows} shows (ms): {string.Join(", ", durations.Select(d => (int)d.TotalMilliseconds))}");
        queue.GetArrayLength().ShouldBe(Shows);
        foreach (var item in queue.EnumerateArray())
        {
            item.EnumerateObject().Select(property => property.Name).ShouldBe(ModerationRequests.QueueItemKeys, ignoreOrder: true, item.ToString());
            item.GetProperty("counts").EnumerateObject().Select(property => property.Name)
                .ShouldBe(ModerationRequests.CountKeys, ignoreOrder: true, item.ToString());
        }

        queue[0].GetProperty("showId").GetGuid().ShouldBe(shows[0], "mais antigos primeiro");
        queue[Shows - 1].GetProperty("showId").GetGuid().ShouldBe(shows[^1], "mais antigos primeiro");
        var first = queue.EnumerateArray().Select(item => item.GetProperty("firstReportedAtUtc").GetDateTimeOffset()).ToList();
        first.ShouldBe(first.Order().ToList(), "a fila vem da primeira denúncia mais antiga para a mais nova");
        var text = queue.ToString();
        authors.ShouldAllBe(author => !text.Contains(author.ToString(), StringComparison.OrdinalIgnoreCase), "a fila identifica quem denunciou");
        text.ShouldNotContain("@subsolo.test", Case.Insensitive, "a fila traz o e-mail de quem denunciou");
    }
}
