using System.Diagnostics;
using System.Net;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0022 IT-02 (§5) — um JPEG de 4000×6000 com quase 10 MB, enviado 10 vezes (10 arquivos diferentes — o mesmo
/// conteúdo visual com um comentário JPEG distinto —, para que cada envio seja processado e não devolvido pelo hash),
/// em sequência, por um artista: o p95 (posto mais próximo) do tempo de resposta fica ≤ 3 s e o pico do working set do
/// processo de teste (que hospeda a API em memória) fica ≤ 300 MB acima do repouso.
/// <para>
/// "Repouso" = o working set depois de a API subir, de um envio de aquecimento (um flyer pequeno, que carrega o
/// libvips e o pipeline) e de uma coleta completa do GC, com as imagens de teste já em memória. O pico é amostrado a
/// cada 10 ms durante os 10 envios (o pico do sistema operacional, <c>PeakWorkingSet64</c>, não volta a zero e incluiria
/// a geração das imagens de teste).
/// </para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class FlyerPerformanceTests(PostgresFixture postgres)
{
    private const int Uploads = 10;

    private static readonly TimeSpan MaxP95 = TimeSpan.FromSeconds(3);

    private const long MaxGrowthBytes = 300L * 1024 * 1024;

    [Fact]
    [Trait("Spec", "SPEC-0022:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task TenUploadsOfA10MegabyteFlyerStayWithin3SecondsP95And300MegabytesOfMemory()
    {
        var ct = TestContext.Current.CancellationToken;
        var large = LargeFlyer.Get();
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (artist, _) = await api.ArtistAsync("artista.desempenho@subsolo.test", ct);
        using var _ = artist;
        var token = await artist.CsrfTokenAsync(ct);
        using (var warmUp = await artist.UploadAsync(TestImages.Jpeg(1080, 1920, seed: 99), ct, csrfToken: token))
        {
            await FlyerUploaded.ReadAsync(warmUp, HttpStatusCode.Created, ct);
        }

        var files = Enumerable.Range(1, Uploads)
            .Select(i => TestImages.WithComment(large, $"SPEC-0022 IT-02 envio {i:D2}"))
            .ToList();
        files.ShouldAllBe(file => file.LongLength <= TestImages.TenMegabytes, "arquivo de teste acima do limite de 10 MB");
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var rest = process.WorkingSet64;

        using var sampling = new CancellationTokenSource();
        var peakTask = SamplePeakAsync(sampling.Token);
        var elapsed = new List<TimeSpan>();
        try
        {
            foreach (var file in files)
            {
                var watch = Stopwatch.StartNew();
                using var response = await artist.UploadAsync(file, ct, csrfToken: token);
                watch.Stop();
                await FlyerUploaded.ReadAsync(response, HttpStatusCode.Created, ct);
                elapsed.Add(watch.Elapsed);
            }
        }
        finally
        {
            await sampling.CancelAsync();
        }

        var peak = await peakTask;
        var p95 = Timing.Percentile(elapsed, 95);
        var growth = peak - rest;
        var report = $"tempos: {string.Join(", ", elapsed.Select(time => $"{time.TotalMilliseconds:F0} ms"))}; " +
            $"repouso {rest / 1048576} MB, pico {peak / 1048576} MB, aumento {growth / 1048576} MB; arquivo {large.Length / 1048576.0:F2} MB";
        TestContext.Current.SendDiagnosticMessage($"SPEC-0022 IT-02: {report}");
        p95.ShouldBeLessThanOrEqualTo(MaxP95, $"p95 acima de 3 s — {report}");
        growth.ShouldBeLessThanOrEqualTo(MaxGrowthBytes, $"pico de memória acima de 300 MB sobre o repouso — {report}");
    }

    /// <summary>Maior working set observado (amostra a cada 10 ms) até o cancelamento.</summary>
    private static async Task<long> SamplePeakAsync(CancellationToken stop)
    {
        using var process = Process.GetCurrentProcess();
        long peak = 0;
        while (!stop.IsCancellationRequested)
        {
            process.Refresh();
            peak = Math.Max(peak, process.WorkingSet64);
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), stop);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        process.Refresh();
        return Math.Max(peak, process.WorkingSet64);
    }

    /// <summary>
    /// O JPEG 4000×6000 de teste, entre 9 e 10 MB (menos uma folga para o comentário): textura suave com ruído fino, cuja
    /// amplitude é ajustada por busca binária até o tamanho cair na faixa. Gerado uma vez por execução e guardado no
    /// diretório temporário do sistema (o arquivo é determinístico: mesmas sementes).
    /// </summary>
    private static class LargeFlyer
    {
        private const int Width = 4000;

        private const int Height = 6000;

        private const long Min = 9L * 1024 * 1024;

        private const long Max = TestImages.TenMegabytes - (16 * 1024);

        private static readonly Lazy<byte[]> Content = new(Generate);

        public static byte[] Get() => Content.Value;

        private static byte[] Generate()
        {
            var cache = Path.Combine(Path.GetTempPath(), "subsolo-spec0022-it02-4000x6000-v1.jpg");
            if (File.Exists(cache) && new FileInfo(cache).Length is >= Min and <= Max)
            {
                return File.ReadAllBytes(cache);
            }

            double low = 0, high = 48;
            byte[]? best = null;
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var detail = (low + high) / 2;
                var candidate = TestImages.Jpeg(Width, Height, seed: 2022, quality: 90, detail: detail);
                if (candidate.LongLength > Max)
                {
                    high = detail;
                    continue;
                }

                best = candidate;
                if (candidate.LongLength >= Min)
                {
                    break;
                }

                low = detail;
            }

            best.ShouldNotBeNull("não foi possível gerar um JPEG 4000×6000 de até 10 MB");
            best.LongLength.ShouldBeGreaterThanOrEqualTo(Min, $"JPEG de teste com só {best.LongLength} bytes (esperado 9–10 MB)");
            TestImages.Measure(best).ShouldBe((Width, Height));
            File.WriteAllBytes(cache, best);
            return best;
        }
    }
}
