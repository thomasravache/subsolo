using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Common.Application.Media;
using Subsolo.Common.Domain;
using Subsolo.Events.UnitTests.Infrastructure;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.CleanupFlyers;
using Subsolo.Modules.Events.Application.Features.UploadFlyer;
using Subsolo.Modules.Events.Domain.Flyers;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0024 H23-01 (revisão da SPEC-0023) — limpeza × reenvio do mesmo arquivo:
/// <list type="bullet">
/// <item>a limpeza apagou a linha do flyer A, e antes de ela apagar os arquivos o mesmo arquivo foi enviado de novo (a
/// linha de A voltou): antes de apagar os arquivos de um id, a limpeza confere que a linha não voltou e, se voltou, mantém
/// os arquivos; os do flyer B, que não voltou, são apagados;</item>
/// <item>o envio de um flyer cuja linha existe mas cujos arquivos sumiram (apagados por uma limpeza no meio) regrava as
/// variantes; com os arquivos no lugar, não reprocessa nada.</item>
/// </list>
/// </summary>
public sealed class FlyerReuploadTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly byte[] Original = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0x00, 0x01, 0x07, 0x09];

    private static string Hash(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    [Fact]
    [Trait("Spec", "SPEC-0024:H23-01")]
    public async Task TheCleanupKeepsTheFilesOfAFlyerWhoseRowCameBackWithAReupload()
    {
        var reuploaded = Flyer.Create(new string('a', 64), Guid.CreateVersion7(), Now.AddHours(-25));
        var gone = Flyer.Create(new string('b', 64), Guid.CreateVersion7(), Now.AddHours(-26));
        var flyers = new ReuploadingFlyers(reuploaded.Id, reuploaded, gone);
        var storage = new MemoryStorage();
        foreach (var path in Flyer.VariantPaths(reuploaded.Id).Concat(Flyer.VariantPaths(gone.Id)))
        {
            storage.Files.Add(path);
        }

        var handler = new CleanupFlyersCommandHandler(
            flyers, storage, new FakeTimeProvider(Now), NullLogger<CleanupFlyersCommandHandler>.Instance, new NoOpFlyerLocks());

        var result = await handler.Handle(new CleanupFlyersCommand(), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        storage.Deleted.ShouldBe(
            Flyer.VariantPaths(gone.Id),
            ignoreOrder: true,
            $"a limpeza apagou os arquivos de um flyer reenviado no meio dela (apagados: {string.Join(", ", storage.Deleted)})");
        foreach (var path in Flyer.VariantPaths(reuploaded.Id))
        {
            storage.Files.ShouldContain(path, $"{path} do flyer reenviado sumiu: a linha voltou e ficou sem arquivo");
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0024:H23-01")]
    public async Task AReuploadOfAFlyerWhoseFilesAreMissingWritesTheVariantsAgain()
    {
        var id = Hash(Original);
        var flyers = new MemoryFlyers(Flyer.Create(id, Guid.CreateVersion7(), Now.AddHours(-2)));
        var storage = new MemoryStorage();
        var processor = new CountingProcessor(id);
        var handler = UploadHandler(processor, storage, flyers);
        using var content = new MemoryStream(Original);

        var result = await handler.Handle(new UploadFlyerCommand(content, Original.Length, Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        result.Value.Flyer.FlyerId.ShouldBe(id);
        foreach (var path in Flyer.VariantPaths(id))
        {
            storage.Files.ShouldContain(path, $"o reenvio não regravou {path}, que faltava (a linha existe, o arquivo não)");
        }

        flyers.Rows.Count(row => row.Id == id).ShouldBe(1, "o reenvio deixou a linha do flyer duplicada ou sem linha");
    }

    [Fact]
    [Trait("Spec", "SPEC-0024:H23-01")]
    public async Task AReuploadOfAFlyerWithItsFilesInPlaceDoesNotProcessAgain()
    {
        var id = Hash(Original);
        var flyers = new MemoryFlyers(Flyer.Create(id, Guid.CreateVersion7(), Now.AddHours(-2)));
        var storage = new MemoryStorage();
        foreach (var path in Flyer.VariantPaths(id))
        {
            storage.Files.Add(path);
        }

        var processor = new CountingProcessor(id);
        var handler = UploadHandler(processor, storage, flyers);
        using var content = new MemoryStream(Original);

        var result = await handler.Handle(new UploadFlyerCommand(content, Original.Length, Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        processor.Calls.ShouldBe(0, "o reenvio com os arquivos no lugar reprocessou a imagem");
        storage.Saves.ShouldBe(0, "o reenvio com os arquivos no lugar regravou variantes");
    }

    private static UploadFlyerCommandHandler UploadHandler(IImageProcessor processor, IFileStorage storage, IFlyerRepository flyers) =>
        new(
            processor,
            storage,
            flyers,
            new NoOpUnitOfWork(),
            new FakeTimeProvider(Now),
            NullLogger<UploadFlyerCommandHandler>.Instance,
            new NoOpFlyerLocks());

    /// <summary>
    /// Flyers em memória: a exclusão condicional apaga as linhas e, logo depois, o mesmo arquivo de
    /// <c>reuploadedId</c> é enviado de novo (a linha volta, com outra data), antes de a limpeza chegar aos arquivos.
    /// </summary>
    private sealed class ReuploadingFlyers(string reuploadedId, params Flyer[] rows) : IFlyerRepository
    {
        private readonly List<Flyer> _rows = [.. rows];

        public Task<Flyer?> GetAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(_rows.FirstOrDefault(row => row.Id == id));

        public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(_rows.Any(row => row.Id == id));

        /// <summary>Morto desde a SPEC-0023 (H23-05); a limpeza não lê a lista.</summary>
        public Task<IReadOnlyList<Flyer>> ListUnattachedCreatedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException("a limpeza não lê a lista dos não ligados");

        public Task<IReadOnlyList<string>> DeleteUnattachedCreatedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken)
        {
            var deleted = _rows.Where(row => row.AttachedAtUtc is null && row.CreatedAtUtc < cutoffUtc).ToList();
            _rows.RemoveAll(deleted.Contains);
            _rows.Add(Flyer.Create(reuploadedId, Guid.CreateVersion7(), Now));
            return Task.FromResult<IReadOnlyList<string>>([.. deleted.Select(row => row.Id)]);
        }

        public void Add(Flyer flyer) => _rows.Add(flyer);

        public void Remove(Flyer flyer) => _rows.Remove(flyer);
    }

    private sealed class MemoryFlyers(params Flyer[] rows) : IFlyerRepository
    {
        public List<Flyer> Rows { get; } = [.. rows];

        public Task<Flyer?> GetAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Rows.FirstOrDefault(row => row.Id == id));

        public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Rows.Any(row => row.Id == id));

        /// <summary>Morto desde a SPEC-0023 (H23-05); o envio não lê a lista.</summary>
        public Task<IReadOnlyList<Flyer>> ListUnattachedCreatedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException("o envio não lê a lista dos não ligados");

        public Task<IReadOnlyList<string>> DeleteUnattachedCreatedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public void Add(Flyer flyer) => Rows.Add(flyer);

        public void Remove(Flyer flyer) => Rows.Remove(flyer);
    }

    /// <summary>Arquivos em memória (só os caminhos): <see cref="IFileStorage.OpenAsync"/> acha o que foi gravado.</summary>
    private sealed class MemoryStorage : IFileStorage
    {
        public HashSet<string> Files { get; } = new(StringComparer.Ordinal);

        public List<string> Deleted { get; } = [];

        public int Saves { get; private set; }

        public Task SaveAsync(string path, Stream content, CancellationToken ct)
        {
            Saves++;
            Files.Add(path);
            return Task.CompletedTask;
        }

        public Task<Stream?> OpenAsync(string path, CancellationToken ct) =>
            Task.FromResult<Stream?>(Files.Contains(path) ? new MemoryStream([1, 2, 3], writable: false) : null);

        public Task DeleteAsync(string path, CancellationToken ct)
        {
            Deleted.Add(path);
            Files.Remove(path);
            return Task.CompletedTask;
        }
    }

    private sealed class CountingProcessor(string contentHash) : IImageProcessor
    {
        public int Calls { get; private set; }

        public Task<Result<FlyerImages>> ProcessFlyerAsync(Stream original, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(Result.Success(new FlyerImages([1], [2], [3], contentHash)));
        }
    }

    private sealed class NoOpUnitOfWork : IEventsUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
