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
/// SPEC-0023 H22-05 (revisão da SPEC-0022) — o handler do upload confere o <see cref="FlyerImages.ContentHash"/> que o
/// processador devolve com o hash que ele mesmo calculou do original (o id do flyer): se forem diferentes, o processador
/// leu outro conteúdo, e nada é gravado (nem variante nem linha). Com o mesmo hash, o upload segue normal.
/// </summary>
public sealed class UploadFlyerContentHashTests
{
    private static readonly byte[] Original = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0x00, 0x01, 0x02, 0x03];

    private static string Hash(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    private static (UploadFlyerCommandHandler Handler, MemoryStorage Storage, MemoryFlyers Flyers) Handler(string contentHash)
    {
        var storage = new MemoryStorage();
        var flyers = new MemoryFlyers();
        var handler = new UploadFlyerCommandHandler(
            new FixedProcessor(contentHash),
            storage,
            flyers,
            new NoOpUnitOfWork(),
            new FakeTimeProvider(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<UploadFlyerCommandHandler>.Instance);
        return (handler, storage, flyers);
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:H22-05")]
    public async Task AContentHashDifferentFromTheHandlersHashIsRejectedWithoutWritingAnything()
    {
        var (handler, storage, flyers) = Handler(new string('0', 64));
        using var content = new MemoryStream(Original);

        Result<FlyerUploadResult>? result = null;
        Exception? thrown = null;
        try
        {
            result = await handler.Handle(new UploadFlyerCommand(content, Original.Length, Guid.CreateVersion7()), TestContext.Current.CancellationToken);
        }
#pragma warning disable CA1031 // o teste aceita tanto a falha como resultado quanto como exceção
        catch (Exception exception)
#pragma warning restore CA1031
        {
            thrown = exception;
        }

        (thrown is not null || result is { IsFailure: true })
            .ShouldBeTrue("o upload seguiu com o ContentHash do processador diferente do hash do original");
        storage.Saved.ShouldBeEmpty("variante gravada com o hash divergente");
        flyers.Added.ShouldBeEmpty("linha gravada com o hash divergente");
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:H22-05")]
    public async Task TheSameContentHashUploadsNormally()
    {
        var (handler, storage, flyers) = Handler(Hash(Original));
        using var content = new MemoryStream(Original);

        var result = await handler.Handle(new UploadFlyerCommand(content, Original.Length, Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        result.Value.Flyer.FlyerId.ShouldBe(Hash(Original));
        storage.Saved.Count.ShouldBe(3);
        flyers.Added.ShouldHaveSingleItem().Id.ShouldBe(Hash(Original));
    }

    private sealed class FixedProcessor(string contentHash) : IImageProcessor
    {
        public Task<Result<FlyerImages>> ProcessFlyerAsync(Stream original, CancellationToken ct) =>
            Task.FromResult(Result.Success(new FlyerImages([1], [2], [3], contentHash)));
    }

    private sealed class MemoryStorage : IFileStorage
    {
        public List<string> Saved { get; } = [];

        public Task SaveAsync(string path, Stream content, CancellationToken ct)
        {
            Saved.Add(path);
            return Task.CompletedTask;
        }

        public Task<Stream?> OpenAsync(string path, CancellationToken ct) => Task.FromResult<Stream?>(null);

        public Task DeleteAsync(string path, CancellationToken ct)
        {
            Saved.Remove(path);
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryFlyers : IFlyerRepository
    {
        public List<Flyer> Added { get; } = [];

        public Task<Flyer?> GetAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Added.FirstOrDefault(flyer => flyer.Id == id));

        public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Added.Any(flyer => flyer.Id == id));

        public Task<IReadOnlyList<Flyer>> ListUnattachedCreatedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Flyer>>([]);

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
