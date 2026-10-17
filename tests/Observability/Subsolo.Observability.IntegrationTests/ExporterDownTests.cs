using System.Diagnostics;
using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Observability.IntegrationTests.Infrastructure;

namespace Subsolo.Observability.IntegrationTests;

/// <summary>
/// SPEC-0009 IT-03 — com o destino OTLP (Seq) inexistente, 50 requisições à agenda respondem 200 e a latência não
/// sobe mais de 5% em relação à API sem exportação (mediana de requisições intercaladas entre as duas APIs; abaixo
/// de <see cref="NoiseFloor"/> de diferença absoluta, a variação é ruído de medição em memória).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ExporterDownTests(PostgresFixture postgres)
{
    private const string AgendaPath = "/api/v1/cities/cidade-exemplo/agenda";
    private const int Requests = 50;
    private const int Warmup = 10;
    private const double MaxOverhead = 0.05;
    private static readonly TimeSpan NoiseFloor = TimeSpan.FromMilliseconds(2);

    [Fact]
    [Trait("Spec", "SPEC-0009:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task AgendaKeepsAnsweringWithoutExtraLatencyWhenSeqIsDown()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await ObservedApi.DatabaseAsync(postgres, ct);
        await using var baseline = new ObservedApiFactory(database);
        await using var seqDown = new ObservedApiFactory(database, ObservedApiFactory.DeadEndpoint());
        using var baselineClient = baseline.CreateClient();
        using var seqDownClient = seqDown.CreateClient();

        for (var i = 0; i < Warmup; i++)
        {
            await TimeAsync(baselineClient, ct);
            await TimeAsync(seqDownClient, ct);
        }

        var baselineTimes = new List<TimeSpan>();
        var seqDownTimes = new List<TimeSpan>();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < Requests; i++)
        {
            var (status, elapsed) = await TimeAsync(seqDownClient, ct);
            statuses.Add(status);
            seqDownTimes.Add(elapsed);
            baselineTimes.Add((await TimeAsync(baselineClient, ct)).Elapsed);
        }

        statuses.ShouldAllBe(status => status == HttpStatusCode.OK, "com o Seq fora do ar, a agenda deixou de responder 200");
        var reference = Median(baselineTimes);
        var observed = Median(seqDownTimes);
        var limit = TimeSpan.FromTicks((long)(reference.Ticks * (1 + MaxOverhead)));
        (observed <= limit || observed - reference <= NoiseFloor).ShouldBeTrue(
            $"mediana com o Seq fora do ar {observed.TotalMilliseconds:F2} ms > {limit.TotalMilliseconds:F2} ms " +
            $"(sem exportação: {reference.TotalMilliseconds:F2} ms + 5%)");
    }

    private static async Task<(HttpStatusCode Status, TimeSpan Elapsed)> TimeAsync(HttpClient client, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        using var response = await client.GetAsync(new Uri(AgendaPath, UriKind.Relative), ct);
        await response.Content.ReadAsByteArrayAsync(ct);
        return (response.StatusCode, watch.Elapsed);
    }

    private static TimeSpan Median(List<TimeSpan> values)
    {
        var sorted = values.Order().ToList();
        return sorted[sorted.Count / 2];
    }
}
