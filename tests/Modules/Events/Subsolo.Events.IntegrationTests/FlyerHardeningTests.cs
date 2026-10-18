using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NetVips;
using Quartz;
using Subsolo.Common.Application.Media;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0023 — itens da revisão da SPEC-0022 (G4 em d47d28b) no Subsolo.Api em memória com o NetVips real:
/// <list type="bullet">
/// <item>H22-01 — reenviar um flyer não ligado renova o <c>created_at_utc</c>: o flyer reenviado 23 h depois do primeiro
/// envio não é apagado pela limpeza 2 h mais tarde (o não reenviado é);</item>
/// <item>H22-03 — o upload fixa <c>MaxRequestBodySize</c> em 10 MiB + a folga do multipart antes de ler o formulário:
/// um corpo <c>chunked</c> (sem <c>Content-Length</c>) acima disso — um arquivo abaixo de 10 MiB com campos a mais —
/// dá 400 <c>events.flyer_too_large</c> (o Kestrel é emulado por <see cref="EmulatedKestrelBodyLimit"/>);</item>
/// <item>H22-04 — o libvips bloqueia os carregadores não confiáveis e todos os que não são JPEG, PNG ou WebP (um TIFF
/// não abre mais); um JPEG que só gera aviso benigno (bytes extras antes de um marcador) é aceito (201);</item>
/// <item>H22-05 — com a fila de processamento ocupada, o envio espera no máximo <c>Media:QueueTimeout</c> (padrão 30 s;
/// 1 s aqui) e responde 503 com o código de serviço indisponível.</item>
/// </list>
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class FlyerHardeningTests(PostgresFixture postgres)
{
    private const string FlyersRoute = "/api/v1/flyers";

    /// <summary>Código do 503 em <c>SecurityProblems.ServiceUnavailable</c> (a spec escreve <c>security.service_unavailable</c>).</summary>
    private const string ServiceUnavailable = "service.unavailable";

    [Fact]
    [Trait("Spec", "SPEC-0023:H22-01")]
    [Trait("Kind", "Integration")]
    public async Task ReuploadingAnUnattachedFlyerRenewsItsCreationTimeAndTheCleanupKeepsIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var start = DateTimeOffset.UtcNow;
        var clock = new FakeTimeProvider(start);
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock);
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var _ = admin;
        var reuploaded = TestImages.Jpeg(400, 600, seed: 71);
        var forgotten = await UploadAsync(admin, TestImages.Jpeg(400, 600, seed: 72), HttpStatusCode.Created, ct);
        var kept = await UploadAsync(admin, reuploaded, HttpStatusCode.Created, ct);

        clock.Advance(TimeSpan.FromHours(23));
        (await UploadAsync(admin, reuploaded, HttpStatusCode.OK, ct)).ShouldBe(kept);
        (await FlyersDatabase.FlyersAsync(api.ConnectionString, ct)).Single(row => row.Id == kept.FlyerId)
            .CreatedAtUtc.ShouldBe(start + TimeSpan.FromHours(23), TimeSpan.FromSeconds(1), "o reenvio não renovou o created_at_utc");

        clock.Advance(TimeSpan.FromHours(2));
        await RunCleanupAsync(api, ct);

        (await Timing.EventuallyAsync(
                async () => (await FlyersDatabase.FlyersAsync(api.ConnectionString, ct)).All(row => row.Id != forgotten.FlyerId),
                TimeSpan.FromSeconds(15),
                ct))
            .ShouldBeTrue("a limpeza não rodou (o flyer não reenviado, de 25 h atrás, continua)");
        (await FlyersDatabase.FlyersAsync(api.ConnectionString, ct)).Select(row => row.Id)
            .ShouldContain(kept.FlyerId, "a limpeza apagou o flyer reenviado há 2 h");
        foreach (var (variant, _, relativePath) in kept.Variants)
        {
            File.Exists(Path.Combine(api.MediaRoot, relativePath)).ShouldBeTrue($"a limpeza apagou o {variant} do flyer reenviado");
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:H22-03")]
    [Trait("Kind", "Integration")]
    public async Task AChunkedBodyAboveTheLimitIs400TooLargeAndTheLimitIsSetBeforeReading()
    {
        var ct = TestContext.Current.CancellationToken;
        var bodyLimit = new EmulatedKestrelBodyLimit();
        await using var api = new FlyersApiFactory(
            await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct),
            configureServices: services => services.AddSingleton<IStartupFilter>(bodyLimit));
        var (artist, _) = await api.ArtistAsync("artista.chunked@subsolo.test", ct);
        using var _ = artist;
        // Arquivo de 9 MiB (abaixo dos 10 MiB por parte) + dois campos de 1 MiB: o corpo passa de 11 MiB.
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(TestImages.JpegPaddedTo(9L * 1024 * 1024));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "flyer.jpg");
        form.Add(new StringContent(new string('x', 1024 * 1024)), "extra1");
        form.Add(new StringContent(new string('y', 1024 * 1024)), "extra2");
        var bytes = await form.ReadAsByteArrayAsync(ct);
        using var chunked = new StreamContent(new UnknownLengthStream(bytes));
        chunked.Headers.ContentType = form.Headers.ContentType;
        chunked.Headers.ContentLength.ShouldBeNull("o corpo do teste devia ir sem Content-Length");

        using var response = await artist.SendAsync(HttpMethod.Post, FlyersRoute, ct, chunked, await artist.CsrfTokenAsync(ct));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "events.flyer_too_large", ct);
        var limit = bodyLimit.LimitsAtFirstRead.ShouldHaveSingleItem("o corpo do upload não foi lido uma vez");
        limit.ShouldNotBeNull("o upload tirou o limite do corpo");
        limit.Value.ShouldBeInRange(
            TestImages.TenMegabytes,
            TestImages.TenMegabytes + (1024 * 1024),
            $"MaxRequestBodySize = {limit} ao começar a ler: devia ser 10 MiB + a folga do multipart, não o padrão do Kestrel");
        api.MediaFiles().ShouldBeEmpty();
        (await FlyersDatabase.FlyersAsync(api.ConnectionString, ct)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:H22-04")]
    [Trait("Kind", "Integration")]
    public async Task OnlyTheJpegPngAndWebPLoadersStayAvailableOnceTheProcessorIsUp()
    {
        var ct = TestContext.Current.CancellationToken;
        byte[] tiff;
        using (var image = Image.Black(16, 16))
        {
            tiff = image.TiffsaveBuffer();
        }

        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        api.Services.GetRequiredService<IImageProcessor>().ShouldNotBeNull();

        Should.Throw<VipsException>(
            () =>
            {
                using var loaded = Image.NewFromBuffer(tiff);
                _ = loaded.Avg();
            },
            "o carregador de TIFF continua liberado no libvips (só JPEG, PNG e WebP deviam abrir)");
        foreach (var (format, content) in new[]
                 {
                     ("jpeg", TestImages.Jpeg(32, 32)),
                     ("png", TestImages.Png(32, 32)),
                     ("webp", TestImages.WebP(32, 32)),
                 })
        {
            Should.NotThrow(
                () =>
                {
                    using var loaded = Image.NewFromBuffer(content);
                    _ = loaded.Avg();
                },
                $"o carregador de {format} foi bloqueado");
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:H22-04")]
    [Trait("Kind", "Integration")]
    public async Task AJpegWithOnlyABenignWarningIsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (artist, _) = await api.ArtistAsync("artista.aviso@subsolo.test", ct);
        using var _ = artist;
        var jpeg = TestImages.JpegWithExtraneousBytesBeforeMarker(600, 900);

        using var response = await artist.UploadAsync(jpeg, ct);

        var flyer = await FlyerUploaded.ReadAsync(response, HttpStatusCode.Created, ct);
        foreach (var (variant, _, relativePath) in flyer.Variants)
        {
            File.Exists(Path.Combine(api.MediaRoot, relativePath)).ShouldBeTrue($"variante {variant} ausente");
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:H22-05")]
    [Trait("Kind", "Integration")]
    public async Task WithTheProcessingQueueBusyTheUploadGivesUpAfterTheQueueTimeoutWith503()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new FlyersApiFactory(
            await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct),
            settings: new Dictionary<string, string>(StringComparer.Ordinal) { ["Media:QueueTimeout"] = "00:00:01" });
        var (artist, _) = await api.ArtistAsync("artista.fila@subsolo.test", ct);
        using var _ = artist;
        var processor = api.Services.GetRequiredService<IImageProcessor>();
        await using var blocking = new BlockingStream();
        // Um processamento que segura a vez na fila (fica lendo um arquivo que não termina).
        var busy = processor.ProcessFlyerAsync(blocking, ct);
        await blocking.Reading.WaitAsync(TimeSpan.FromSeconds(10), ct);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var watch = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        try
        {
            response = await artist.UploadAsync(TestImages.Jpeg(400, 600, seed: 81), timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Esperou mais de 20 s: a fila não tem tempo limite.
        }
        finally
        {
            blocking.Release();
        }

        using (response)
        {
            response.ShouldNotBeNull($"o envio esperou a fila por {watch.Elapsed.TotalSeconds:F0} s sem desistir (sem tempo limite)");
            await response.ShouldBeProblemAsync(HttpStatusCode.ServiceUnavailable, ServiceUnavailable, ct);
            watch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10), "a desistência demorou bem mais que Media:QueueTimeout (1 s)");
        }

        await busy;
        api.MediaFiles().ShouldBeEmpty("o envio que desistiu gravou arquivo");
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:H22-05")]
    [Trait("Kind", "Integration")]
    public async Task TheQueueTimeoutDefaultsToThirtySeconds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

        var options = api.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Subsolo.Common.Infrastructure.Media.MediaOptions>>().Value;

        options.QueueTimeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    private static async Task<FlyerUploaded> UploadAsync(FlyersApiClient client, byte[] content, HttpStatusCode status, CancellationToken ct)
    {
        using var response = await client.UploadAsync(content, ct);
        return await FlyerUploaded.ReadAsync(response, status, ct);
    }

    /// <summary>Dispara a tarefa periódica de limpeza dos flyers (como no <c>FlyerCleanupTests</c> da SPEC-0022).</summary>
    private static async Task RunCleanupAsync(FlyersApiFactory api, CancellationToken ct)
    {
        var scheduler = await api.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(ct);
        var jobs = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), ct);
        var cleanup = jobs.Single(job =>
            job.Name.Contains("Flyer", StringComparison.OrdinalIgnoreCase) && job.Name.Contains("Cleanup", StringComparison.OrdinalIgnoreCase));
        await scheduler.TriggerJob(cleanup, new JobDataMap(), ct);
    }

    /// <summary>Corpo de tamanho desconhecido (o cliente manda <c>chunked</c>, sem <c>Content-Length</c>).</summary>
    private sealed class UnknownLengthStream(byte[] content) : MemoryStream(content, writable: false)
    {
        public override bool CanSeek => false;
    }

    /// <summary>Um arquivo que não termina até <see cref="Release"/>: avisa quando começam a ler (<see cref="Reading"/>).</summary>
    private sealed class BlockingStream : Stream
    {
        private readonly TaskCompletionSource _reading = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Reading => _reading.Task;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => 0;
            set => throw new NotSupportedException();
        }

        public void Release() => _released.TrySetResult();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _reading.TrySetResult();
            await _released.Task.WaitAsync(cancellationToken);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
