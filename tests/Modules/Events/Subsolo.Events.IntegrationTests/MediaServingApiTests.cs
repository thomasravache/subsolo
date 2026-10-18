using System.Net;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0022 IT-06 — no modo de desenvolvimento a API serve <c>/media/flyers/*</c> do <c>Media:Root</c> para qualquer
/// um (sem sessão): a variante existente dá 200 com o tipo certo (<c>image/webp</c>, <c>image/jpeg</c>),
/// <c>Cache-Control: public, max-age=31536000, immutable</c> e <c>X-Content-Type-Options: nosniff</c>; um nome
/// inexistente dá 404; um caminho com <c>..</c> dá 404 e não entrega arquivo fora do <c>Media:Root</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class MediaServingApiTests(PostgresFixture postgres)
{
    private const string Secret = "segredo-fora-do-media-root";

    private async Task<FlyersApiFactory> FactoryAsync(CancellationToken ct) =>
        new(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

    [Fact]
    [Trait("Spec", "SPEC-0022:IT-06")]
    [Trait("Kind", "Integration")]
    public async Task AnUploadedVariantIsServedPubliclyWithImmutableCacheAndNosniff()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (artist, _) = await api.ArtistAsync("artista.media@subsolo.test", ct);
        using var _ = artist;
        using var anonymous = api.CreateFlyersClient();
        FlyerUploaded flyer;
        using (var upload = await artist.UploadAsync(TestImages.Jpeg(1080, 1920), ct))
        {
            flyer = await FlyerUploaded.ReadAsync(upload, HttpStatusCode.Created, ct);
        }

        var expectedTypes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["thumb"] = "image/webp",
            ["full"] = "image/webp",
            ["og"] = "image/jpeg",
        };
        foreach (var (variant, url, relativePath) in flyer.Variants)
        {
            using var response = await anonymous.GetAsync(url, ct);

            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {url}");
            response.Content.Headers.ContentType?.MediaType.ShouldBe(expectedTypes[variant], $"GET {url}");
            ShouldBeImmutable(response, url);
            (await response.Content.ReadAsByteArrayAsync(ct))
                .ShouldBe(await File.ReadAllBytesAsync(Path.Combine(api.MediaRoot, relativePath), ct), $"GET {url} entregou outro arquivo");
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0022:IT-06")]
    [Trait("Kind", "Integration")]
    public async Task AMissingNameIs404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        using var anonymous = api.CreateFlyersClient();
        var route = $"/media/flyers/{new string('a', 64)}-thumb.webp";

        using var response = await anonymous.GetAsync(route, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, $"GET {route}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0022:IT-06")]
    [Trait("Kind", "Integration")]
    public async Task APathWithDotDotIs404AndNeverLeaksFilesOutsideTheMediaRoot()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        await File.WriteAllTextAsync(Path.Combine(api.Sandbox, "secret.txt"), Secret, ct);
        await File.WriteAllTextAsync(Path.Combine(api.MediaRoot, "secret.txt"), Secret, ct);
        using var anonymous = api.CreateFlyersClient();
        (await RouteServesFlyersAsync(api, anonymous, ct))
            .ShouldBeTrue("a API não serve /media/flyers/* do Media:Root (sem isso, o 404 abaixo não prova nada)");

        foreach (var route in new[]
        {
            "/media/flyers/..%2F..%2Fsecret.txt",
            "/media/flyers/..%2f..%2fsecret.txt",
            "/media/flyers/..%2Fsecret.txt",
            "/media/flyers/%2e%2e%2f%2e%2e%2fsecret.txt",
            "/media/flyers/..%5C..%5Csecret.txt",
        })
        {
            using var response = await anonymous.GetAsync(route, ct);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound, $"GET {route}");
            (await response.Content.ReadAsStringAsync(ct))
                .ShouldNotContain(Secret, Case.Sensitive, $"GET {route} vazou arquivo de fora de /media/flyers");
        }
    }

    /// <summary>Um arquivo posto à mão em <c>Media:Root/flyers</c> é servido (a rota existe).</summary>
    private static async Task<bool> RouteServesFlyersAsync(FlyersApiFactory api, FlyersApiClient client, CancellationToken ct)
    {
        var flyers = Path.Combine(api.MediaRoot, "flyers");
        Directory.CreateDirectory(flyers);
        var name = $"{new string('b', 64)}-thumb.webp";
        await File.WriteAllBytesAsync(Path.Combine(flyers, name), TestImages.WebP(40, 60), ct);
        using var response = await client.GetAsync($"/media/flyers/{name}", ct);
        return response.StatusCode == HttpStatusCode.OK;
    }

    private static void ShouldBeImmutable(HttpResponseMessage response, string url)
    {
        var cacheControl = response.Headers.CacheControl.ShouldNotBeNull($"GET {url} sem Cache-Control");
        cacheControl.Public.ShouldBeTrue($"GET {url}: Cache-Control {cacheControl} sem public");
        cacheControl.MaxAge.ShouldBe(TimeSpan.FromSeconds(31536000), $"GET {url}: Cache-Control {cacheControl}");
        cacheControl.Extensions.Select(extension => extension.Name)
            .ShouldContain("immutable", $"GET {url}: Cache-Control {cacheControl} sem immutable");
        cacheControl.NoCache.ShouldBeFalse($"GET {url}: Cache-Control {cacheControl}");
        cacheControl.Private.ShouldBeFalse($"GET {url}: Cache-Control {cacheControl}");
        response.Headers.TryGetValues("X-Content-Type-Options", out var nosniff).ShouldBeTrue($"GET {url} sem X-Content-Type-Options");
        nosniff.ShouldBe(["nosniff"], $"GET {url}");
    }
}
