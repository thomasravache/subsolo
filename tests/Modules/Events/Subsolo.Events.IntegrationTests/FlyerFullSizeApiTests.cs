using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Application.Media;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0026 H24-01 (revisão da SPEC-0024) — o processador de imagem registrado na API (o <c>NetVipsImageProcessor</c>)
/// devolve as medidas da variante <c>full</c> em <see cref="FlyerImages.FullSize"/>, iguais às da imagem gerada: um
/// flyer largo é reduzido para 1080 px de largura, e um mais estreito que 1080 px fica com a largura original.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class FlyerFullSizeApiTests(PostgresFixture postgres)
{
    [Theory]
    [Trait("Spec", "SPEC-0026:H24-01")]
    [Trait("Kind", "Integration")]
    [InlineData(2000, 1000, 1080, 540)]
    [InlineData(300, 400, 300, 400)]
    public async Task TheImageProcessorGivesTheSizeOfTheFullVariant(int width, int height, int fullWidth, int fullHeight)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var processor = api.Services.GetRequiredService<IImageProcessor>();
        using var original = new MemoryStream(TestImages.Jpeg(width, height, seed: 91));

        var result = await processor.ProcessFlyerAsync(original, ct);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        result.Value.FullSize.ShouldBe(new PixelSize(fullWidth, fullHeight), $"medidas do full de um flyer {width}×{height}");
        using var full = NetVips.Image.NewFromBuffer(result.Value.Full);
        result.Value.FullSize.ShouldBe(new PixelSize(full.Width, full.Height), "FullSize diferente da variante full gerada");
    }
}
