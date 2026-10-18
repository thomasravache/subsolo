using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Quartz;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Events.Application;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0022 IT-07 — a tarefa periódica de limpeza (job do Quartz do módulo Events) apaga os flyers não ligados a show
/// criados há mais de 24 h — arquivos e linha — e mantém o ligado (<see cref="IFlyerCatalog.AttachAsync"/>) e o não
/// ligado recente. Tempo controlado com <see cref="FakeTimeProvider"/>; a tarefa é disparada pelo teste; quem envia é o
/// admin (também pode enviar, IT-04).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class FlyerCleanupTests(PostgresFixture postgres)
{
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    [Trait("Spec", "SPEC-0022:IT-07")]
    [Trait("Kind", "Integration")]
    public async Task TheCleanupDeletesAnUnattachedFlyerFrom25HoursAgoAndKeepsTheAttachedAndTheRecentOnes()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock);
        // O admin envia: com o relógio parado, a outbox não entregaria o papel artist (concedido por evento).
        var (artist, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var _ = artist;
        var orphan = await UploadAsync(artist, TestImages.Jpeg(600, 900, seed: 11), ct);
        var attached = await UploadAsync(artist, TestImages.Jpeg(600, 900, seed: 12), ct);
        await using (var scope = api.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IFlyerCatalog>().AttachAsync(attached.FlyerId, ct);
            await scope.ServiceProvider.GetRequiredService<IEventsUnitOfWork>().SaveChangesAsync(ct);
        }

        clock.Advance(TimeSpan.FromHours(24));
        var recent = await UploadAsync(artist, TestImages.Jpeg(600, 900, seed: 13), ct);
        clock.Advance(TimeSpan.FromHours(1));

        await RunCleanupAsync(api, ct);

        (await Timing.EventuallyAsync(
                async () => (await FlyersDatabase.FlyersAsync(api.ConnectionString, ct)).All(row => row.Id != orphan.FlyerId),
                CleanupTimeout,
                ct))
            .ShouldBeTrue("o flyer sem show criado há 25 h não foi apagado do banco");
        foreach (var (variant, _, relativePath) in orphan.Variants)
        {
            File.Exists(Path.Combine(api.MediaRoot, relativePath)).ShouldBeFalse($"o {variant} do flyer apagado ficou no disco");
        }

        var rows = await FlyersDatabase.FlyersAsync(api.ConnectionString, ct);
        rows.Select(row => row.Id).ShouldBe([attached.FlyerId, recent.FlyerId], ignoreOrder: true);
        rows.Single(row => row.Id == attached.FlyerId).AttachedAtUtc.ShouldNotBeNull("o flyer ligado tem attached_at_utc");
        foreach (var kept in new[] { attached, recent })
        {
            foreach (var (variant, _, relativePath) in kept.Variants)
            {
                File.Exists(Path.Combine(api.MediaRoot, relativePath)).ShouldBeTrue($"o {variant} de {kept.FlyerId} foi apagado");
            }
        }

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<IFlyerCatalog>();
            (await catalog.ExistsAsync(orphan.FlyerId, ct)).ShouldBeFalse("o catálogo ainda acha o flyer apagado");
            (await catalog.ExistsAsync(attached.FlyerId, ct)).ShouldBeTrue("o catálogo não acha o flyer ligado");
        }
    }

    private static async Task<FlyerUploaded> UploadAsync(FlyersApiClient artist, byte[] content, CancellationToken ct)
    {
        using var response = await artist.UploadAsync(content, ct);
        return await FlyerUploaded.ReadAsync(response, HttpStatusCode.Created, ct);
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
        cleanup.ShouldHaveSingleItem(
            $"nenhuma (ou mais de uma) tarefa periódica de limpeza dos flyers agendada (jobs: {string.Join(", ", jobs.Select(job => job.Name))})");
        await scheduler.TriggerJob(cleanup[0], new JobDataMap(), ct);
    }
}
