using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Common.Application.Media;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.UploadFlyer;
using Subsolo.Modules.Events.Domain.Flyers;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0026 H24-01 (revisão da SPEC-0024) — as medidas da variante <c>full</c> gravadas na linha do flyer vêm do
/// processador de imagem (<see cref="FlyerImages.FullSize"/>, dadas pelo <c>NetVipsImageProcessor</c>), não da leitura
/// do cabeçalho WebP: com bytes do <c>full</c> que nem são WebP, a linha fica com as medidas que o processador informou.
/// A leitura do cabeçalho (<c>WebPSize</c>) sai do módulo.
/// </summary>
public sealed class FlyerFullSizeTests
{
    private static readonly byte[] Original = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0x00, 0x01, 0x04, 0x05];

    [Fact]
    [Trait("Spec", "SPEC-0026:H24-01")]
    public async Task TheFlyerRowGetsTheFullSizeGivenByTheImageProcessor()
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Original));
        var flyers = new MemoryFlyers();
        var handler = new UploadFlyerCommandHandler(
            new SizedProcessor(hash, new PixelSize(1080, 1350)),
            new MemoryStorage(),
            flyers,
            new NoOpUnitOfWork(),
            new FakeTimeProvider(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<UploadFlyerCommandHandler>.Instance);
        using var content = new MemoryStream(Original);

        var result = await handler.Handle(new UploadFlyerCommand(content, Original.Length, Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        var flyer = flyers.Added.ShouldHaveSingleItem();
        flyer.FullWidth.ShouldBe(1080, "a largura do full não veio do processador");
        flyer.FullHeight.ShouldBe(1350, "a altura do full não veio do processador");
    }

    [Fact]
    [Trait("Spec", "SPEC-0026:H24-01")]
    public void TheWebPHeaderReaderIsGone()
    {
        typeof(UploadFlyerCommand).Assembly
            .GetType("Subsolo.Modules.Events.Application.Features.UploadFlyer.WebPSize", throwOnError: false)
            .ShouldBeNull("WebPSize continua no Events: as medidas do full vêm do processador (FlyerImages)");
    }

    /// <summary>Processador que devolve variantes que não são imagens de verdade, com as medidas do <c>full</c> informadas.</summary>
    private sealed class SizedProcessor(string contentHash, PixelSize fullSize) : IImageProcessor
    {
        public Task<Result<FlyerImages>> ProcessFlyerAsync(Stream original, CancellationToken ct) =>
            Task.FromResult(Result.Success(new FlyerImages([1], [2], [3], contentHash, fullSize)));
    }

    private sealed class MemoryStorage : IFileStorage
    {
        public Task SaveAsync(string path, Stream content, CancellationToken ct) => Task.CompletedTask;

        public Task<Stream?> OpenAsync(string path, CancellationToken ct) => Task.FromResult<Stream?>(null);

        public Task DeleteAsync(string path, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class MemoryFlyers : IFlyerRepository
    {
        public List<Flyer> Added { get; } = [];

        public Task<Flyer?> GetAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Added.FirstOrDefault(flyer => flyer.Id == id));

        public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Added.Any(flyer => flyer.Id == id));

        public Task<IReadOnlyList<string>> DeleteUnattachedCreatedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public void Add(Flyer flyer) => Added.Add(flyer);

        public void Remove(Flyer flyer) => Added.Remove(flyer);
    }

    private sealed class NoOpUnitOfWork : IEventsUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
