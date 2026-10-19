using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Quartz;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0026 H24-02 (revisão da SPEC-0024) — a janela entre a limpeza dos flyers e um envio simultâneo do mesmo arquivo
/// fica fechada: com o <see cref="CleanupLockRace"/>, o envio chega depois de a limpeza apagar a linha e conferir que ela
/// não voltou, mas antes de ela apagar os arquivos. No fim, com a linha de volta (o envio a gravou), os três arquivos
/// estão no disco: a checagem e a exclusão da limpeza e a gravação e a inserção do envio acontecem sob a mesma trava
/// (<c>pg_advisory_xact_lock</c> por id), e uma nunca corta a outra ao meio. Tempo controlado com
/// <see cref="FakeTimeProvider"/>; a tarefa de limpeza é disparada pelo teste.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class FlyerCleanupLockTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    [Fact]
    [Trait("Spec", "SPEC-0026:H24-02")]
    [Trait("Kind", "Integration")]
    public async Task AnUploadDuringTheCleanupNeverLeavesTheRowWithoutItsFiles()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var race = new CleanupLockRace();
        await using var api = new FlyersApiFactory(
            await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct),
            clock,
            configureServices: race.Install);
        // O admin envia: com o relógio parado, a outbox não entregaria o papel artist (concedido por evento).
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        var (otherTab, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var _ = admin;
        using var __ = otherTab;
        var image = TestImages.Jpeg(600, 900, seed: 81);
        FlyerUploaded flyer;
        using (var response = await admin.UploadAsync(image, ct))
        {
            flyer = await FlyerUploaded.ReadAsync(response, HttpStatusCode.Created, ct);
        }

        clock.Advance(TimeSpan.FromHours(25));
        race.Arm(flyer.FlyerId, async () =>
        {
            using var again = await otherTab.UploadAsync(image, ct);
            return again.StatusCode;
        });

        await RunCleanupAsync(api, ct);

        (await Timing.EventuallyAsync(
                () => Task.FromResult(race.Upload is { IsCompleted: true } && race.Deleted >= flyer.Variants.Count),
                Timeout,
                ct))
            .ShouldBeTrue($"a corrida não terminou (envio disparado: {race.Upload is not null}; arquivos apagados pela limpeza: {race.Deleted})");
        (await race.Upload!).ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

        var rows = await FlyersDatabase.FlyersAsync(api.ConnectionString, ct);
        rows.Select(row => row.Id).ShouldContain(flyer.FlyerId, "o envio no meio da limpeza não deixou a linha do flyer");
        foreach (var (variant, _, relativePath) in flyer.Variants)
        {
            File.Exists(Path.Combine(api.MediaRoot, relativePath))
                .ShouldBeTrue($"a linha do flyer voltou, mas a limpeza apagou o {variant} gravado pelo envio (sem a trava por id)");
        }
    }

    /// <summary>Dispara a tarefa periódica de limpeza dos flyers (um job do Quartz agendado no host).</summary>
    private static async Task RunCleanupAsync(FlyersApiFactory api, CancellationToken ct)
    {
        var scheduler = await api.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(ct);
        var jobs = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), ct);
        var cleanup = jobs.Where(job =>
                job.Name.Contains("Flyer", StringComparison.OrdinalIgnoreCase)
                && job.Name.Contains("Cleanup", StringComparison.OrdinalIgnoreCase))
            .ToList();
        cleanup.ShouldHaveSingleItem($"nenhuma (ou mais de uma) tarefa de limpeza dos flyers (jobs: {string.Join(", ", jobs.Select(job => job.Name))})");
        await scheduler.TriggerJob(cleanup[0], new JobDataMap(), ct);
    }
}
