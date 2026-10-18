using System.Net;
using System.Security.Cryptography;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0022 — <c>POST /api/v1/flyers</c> no Subsolo.Api em memória com o PostgreSQL e o NetVips reais e um diretório
/// temporário como <c>Media:Root</c>:
/// <list type="bullet">
/// <item>IT-01 — um JPEG 1080×1920 com EXIF e GPS enviado por um artista dá 201 com <c>flyerId</c> = SHA-256 do original
/// e as URLs <c>/media/flyers/&lt;id&gt;-thumb.webp</c>, <c>-full.webp</c> e <c>-og.jpg</c>; as três variantes estão em
/// <c>Media:Root/flyers</c> com as medidas do UT-02 (400×711, 1080×1920, 1200×630), sem EXIF nem GPS, e
/// <c>events.flyers</c> tem a linha; o mesmo arquivo de novo dá 200 com o mesmo corpo, sem regravar os arquivos;</item>
/// <item>IT-03 — PDF com nome <c>.jpg</c>, 10 MB + 1 e PNG 8000×8000 dão 400 <c>flyer_invalid_type</c>,
/// <c>flyer_too_large</c> e <c>flyer_too_many_pixels</c> (e sem o campo <c>file</c>, <c>flyer_missing</c>), sem gravar nada;</item>
/// <item>IT-05 — um JPEG de cabeçalho válido e dados quebrados dá 422 <c>flyer_unprocessable</c> sem deixar arquivo nem linha.</item>
/// </list>
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class UploadFlyerApiTests(PostgresFixture postgres)
{
    private async Task<FlyersApiFactory> FactoryAsync(CancellationToken ct) =>
        new(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

    [Fact]
    [Trait("Spec", "SPEC-0022:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task AnArtistUploadsAJpegWithGpsAndGetsThreeCleanVariantsNamedByTheHash()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (artist, artistId) = await api.ArtistAsync("artista.flyer@subsolo.test", ct);
        using var _ = artist;
        var original = TestImages.JpegWithGps(1080, 1920);
        var expectedId = Convert.ToHexStringLower(SHA256.HashData(original));

        using var response = await artist.UploadAsync(original, ct, fileName: "Flyer do Show (final).JPG");

        var flyer = await FlyerUploaded.ReadAsync(response, HttpStatusCode.Created, ct);
        flyer.FlyerId.ShouldBe(expectedId, "o id é o SHA-256 (hex) do arquivo original");
        flyer.ThumbUrl.ShouldBe($"/media/flyers/{expectedId}-thumb.webp");
        flyer.FullUrl.ShouldBe($"/media/flyers/{expectedId}-full.webp");
        flyer.OgUrl.ShouldBe($"/media/flyers/{expectedId}-og.jpg");

        var expectedSizes = new Dictionary<string, (int Width, int Height)>(StringComparer.Ordinal)
        {
            ["thumb"] = (400, 711),
            ["full"] = (1080, 1920),
            ["og"] = (1200, 630),
        };
        var writes = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        foreach (var (variant, _, relativePath) in flyer.Variants)
        {
            var path = Path.Combine(api.MediaRoot, relativePath);
            File.Exists(path).ShouldBeTrue($"variante {variant} ausente em Media:Root ({relativePath}); arquivos: {string.Join(", ", api.MediaFiles())}");
            var content = await File.ReadAllBytesAsync(path, ct);
            var (width, height) = TestImages.Measure(content);
            width.ShouldBe(expectedSizes[variant].Width, $"largura do {variant}");
            height.ShouldBeInRange(expectedSizes[variant].Height - 1, expectedSizes[variant].Height + 1, $"altura do {variant}");
            TestImages.Metadata(content).ShouldBeEmpty($"o {variant} guardou metadados do original (EXIF/GPS/XMP/ICC)");
            writes[variant] = File.GetLastWriteTimeUtc(path);
        }

        VariantBytes(flyer, "thumb").AsSpan(0, 4).SequenceEqual("RIFF"u8).ShouldBeTrue("thumb em WebP");
        VariantBytes(flyer, "full").AsSpan(0, 4).SequenceEqual("RIFF"u8).ShouldBeTrue("full em WebP");
        VariantBytes(flyer, "og").AsSpan(0, 2).SequenceEqual((ReadOnlySpan<byte>)[0xFF, 0xD8]).ShouldBeTrue("og em JPEG");
        string.Join(", ", api.MediaFiles()).ShouldBe(
            string.Join(", ", flyer.Variants.Select(variant => variant.RelativePath).Order(StringComparer.Ordinal)),
            "o original (com EXIF) não é guardado; só as três variantes, sem nome do usuário");

        var row = (await FlyersDatabase.FlyersAsync(api.ConnectionString, ct)).ShouldHaveSingleItem("events.flyers sem a linha do flyer");
        row.Id.ShouldBe(expectedId);
        row.UploadedBy.ShouldBe(artistId);
        row.AttachedAtUtc.ShouldBeNull("flyer recém-enviado não está ligado a show");

        await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
        using var again = await artist.UploadAsync(original, ct, fileName: "outro-nome.jpg");

        var repeated = await FlyerUploaded.ReadAsync(again, HttpStatusCode.OK, ct);
        repeated.ShouldBe(flyer, "o mesmo arquivo devolve o mesmo id e as mesmas URLs");
        foreach (var (variant, _, relativePath) in flyer.Variants)
        {
            File.GetLastWriteTimeUtc(Path.Combine(api.MediaRoot, relativePath))
                .ShouldBe(writes[variant], $"o {variant} foi regravado (reprocessou o mesmo arquivo)");
        }

        (await FlyersDatabase.FlyersAsync(api.ConnectionString, ct)).ShouldHaveSingleItem("o reenvio criou outra linha");

        byte[] VariantBytes(FlyerUploaded uploaded, string variant) =>
            File.ReadAllBytes(Path.Combine(api.MediaRoot, uploaded.Variants.Single(item => item.Variant == variant).RelativePath));
    }

    [Fact]
    [Trait("Spec", "SPEC-0022:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task FakeTypeTooLargeAndTooManyPixelsAreRejectedWithoutWritingAnything()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (artist, _) = await api.ArtistAsync("artista.invalido@subsolo.test", ct);
        using var _ = artist;

        using (var pdf = await artist.UploadAsync(TestImages.Pdf(), ct, fileName: "flyer.jpg", contentType: "image/jpeg"))
        {
            await pdf.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "events.flyer_invalid_type", ct);
        }

        using (var tooLarge = await artist.UploadAsync(TestImages.JpegPaddedTo(TestImages.TenMegabytes + 1), ct))
        {
            await tooLarge.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "events.flyer_too_large", ct);
        }

        using (var tooManyPixels = await artist.UploadAsync(TestImages.Png(8000, 8000), ct, fileName: "flyer.png", contentType: "image/png"))
        {
            await tooManyPixels.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "events.flyer_too_many_pixels", ct);
        }

        using (var missing = await artist.UploadAsync(TestImages.Jpeg(400, 600), ct, field: "imagem"))
        {
            await missing.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "events.flyer_missing", ct);
        }

        api.MediaFiles().ShouldBeEmpty("upload rejeitado não grava arquivo (nem temporário)");
        (await FlyersDatabase.FlyersAsync(api.ConnectionString, ct)).ShouldBeEmpty("upload rejeitado não grava linha");
    }

    [Fact]
    [Trait("Spec", "SPEC-0022:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task ACorruptJpegIsUnprocessableAndLeavesNothingBehind()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (artist, _) = await api.ArtistAsync("artista.corrompido@subsolo.test", ct);
        using var _ = artist;
        var corrupt = TestImages.CorruptJpeg(1080, 1920);
        TestImages.Measure(corrupt).ShouldBe((1080, 1920), "o cabeçalho do JPEG de teste continua legível");

        using var response = await artist.UploadAsync(corrupt, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "events.flyer_unprocessable", ct);
        api.MediaFiles().ShouldBeEmpty("a falha no processamento deixou arquivos (pela metade ou temporários)");
        (await FlyersDatabase.FlyersAsync(api.ConnectionString, ct)).ShouldBeEmpty("a falha no processamento gravou a linha");
    }
}
