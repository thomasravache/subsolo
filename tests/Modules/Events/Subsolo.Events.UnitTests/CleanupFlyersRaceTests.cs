using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Common.Application.Media;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.CleanupFlyers;
using Subsolo.Modules.Events.Domain.Flyers;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0023 H22-01 (revisão da SPEC-0022) — a limpeza dos flyers apaga primeiro as linhas, com a condição no banco
/// (<c>attached_at_utc IS NULL AND created_at_utc &lt; corte</c>), e só depois os arquivos das linhas apagadas. Dublê do
/// repositório que reproduz a corrida: o flyer B estava sem show quando a limpeza começou e é ligado a um show logo
/// depois da leitura; a limpeza não pode apagar nem a linha nem os arquivos dele. O antigo sem show (A) é apagado e o
/// recente (C) fica.
/// </summary>
public sealed class CleanupFlyersRaceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Spec", "SPEC-0023:H22-01")]
    public async Task AFlyerAttachedWhileTheCleanupRunsKeepsItsRowAndItsFiles()
    {
        var log = new List<string>();
        var stale = Flyer.Create(new string('a', 64), Guid.CreateVersion7(), Now.AddHours(-25));
        var racing = Flyer.Create(new string('b', 64), Guid.CreateVersion7(), Now.AddHours(-26));
        var recent = Flyer.Create(new string('c', 64), Guid.CreateVersion7(), Now.AddHours(-1));
        var flyers = new RacingFlyers(log, racing, stale, racing, recent);
        var storage = new RecordingStorage(log);
        var handler = new CleanupFlyersCommandHandler(
            flyers,
            storage,
            new RecordingUnitOfWork(log),
            new FakeTimeProvider(Now),
            NullLogger<CleanupFlyersCommandHandler>.Instance);

        var result = await handler.Handle(new CleanupFlyersCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        storage.Deleted.ShouldBe(Flyer.VariantPaths(stale.Id), ignoreOrder: true, $"arquivos apagados: {string.Join(", ", storage.Deleted)}");
        flyers.Rows.Select(row => row.Id).ShouldBe([racing.Id, recent.Id], ignoreOrder: true, "a linha do flyer ligado no meio da limpeza sumiu");
        var firstFile = log.FindIndex(entry => entry.StartsWith("file:", StringComparison.Ordinal));
        var rowsDeleted = log.FindIndex(entry => entry.StartsWith("rows:", StringComparison.Ordinal));
        rowsDeleted.ShouldBeGreaterThanOrEqualTo(0, $"as linhas não foram apagadas pela exclusão condicional (passos: {string.Join(" → ", log)})");
        rowsDeleted.ShouldBeLessThan(firstFile, $"os arquivos foram apagados antes das linhas (passos: {string.Join(" → ", log)})");
    }

    /// <summary>
    /// Flyers em memória: a leitura dos não ligados devolve o que estava sem show naquele instante e, em seguida, liga
    /// <c>racing</c> a um show (o cadastro que chega no meio da limpeza). A exclusão condicional confere a condição na hora.
    /// </summary>
    private sealed class RacingFlyers(List<string> log, Flyer racing, params Flyer[] rows) : IFlyerRepository
    {
        private readonly List<Flyer> _rows = [.. rows];

        public IReadOnlyList<Flyer> Rows => _rows;

        public Task<Flyer?> GetAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(_rows.FirstOrDefault(row => row.Id == id));

        public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(_rows.Any(row => row.Id == id));

        public Task<IReadOnlyList<Flyer>> ListUnattachedCreatedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
        {
            log.Add("list");
            IReadOnlyList<Flyer> unattached = [.. _rows.Where(row => row.AttachedAtUtc is null && row.CreatedAtUtc < cutoffUtc)];
            Race();
            return Task.FromResult(unattached);
        }

        public Task<IReadOnlyList<string>> DeleteUnattachedCreatedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
        {
            Race();
            var deleted = _rows.Where(row => row.AttachedAtUtc is null && row.CreatedAtUtc < cutoffUtc).ToList();
            _rows.RemoveAll(deleted.Contains);
            log.Add($"rows:{string.Join(",", deleted.Select(row => row.Id[..1]))}");
            return Task.FromResult<IReadOnlyList<string>>([.. deleted.Select(row => row.Id)]);
        }

        public void Add(Flyer flyer) => _rows.Add(flyer);

        public void Remove(Flyer flyer)
        {
            log.Add($"remove:{flyer.Id[..1]}");
            _rows.Remove(flyer);
        }

        /// <summary>O cadastro de show liga o flyer <c>racing</c> (uma vez só).</summary>
        private void Race() => racing.Attach(Now);
    }

    private sealed class RecordingStorage(List<string> log) : IFileStorage
    {
        public List<string> Deleted { get; } = [];

        public Task SaveAsync(string path, Stream content, CancellationToken ct) => Task.CompletedTask;

        public Task<Stream?> OpenAsync(string path, CancellationToken ct) => Task.FromResult<Stream?>(null);

        public Task DeleteAsync(string path, CancellationToken ct)
        {
            log.Add($"file:{path}");
            Deleted.Add(path);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingUnitOfWork(List<string> log) : IEventsUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            log.Add("save");
            return Task.CompletedTask;
        }
    }
}
