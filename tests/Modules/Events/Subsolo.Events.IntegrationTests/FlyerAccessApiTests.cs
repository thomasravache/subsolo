using System.Net;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0022 IT-04 — quem pode enviar flyer: anônimo 401 <c>auth.required</c>, fã 403 <c>auth.forbidden</c>, artista e
/// admin 201; sem <c>X-CSRF</c>, 400 <c>security.csrf_invalid</c>; com os limites de produção, o 11º envio na hora pelo
/// mesmo IP dá 429 <c>security.rate_limited</c> (limite <c>report</c>, 10/h). Nenhum dos recusados grava arquivo.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class FlyerAccessApiTests(PostgresFixture postgres)
{
    private async Task<FlyersApiFactory> FactoryAsync(CancellationToken ct, bool productionRateLimits = false) =>
        new(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), productionRateLimits: productionRateLimits);

    [Fact]
    [Trait("Spec", "SPEC-0022:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task AnonymousIs401FanIs403ArtistAndAdminAre201()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        using var anonymous = api.CreateFlyersClient();
        var (fan, _) = await api.SignedInAsync("fa.sem.perfil@subsolo.test", ct);
        var (artist, _) = await api.ArtistAsync("artista.acesso@subsolo.test", ct);
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var _ = fan;
        using var __ = artist;
        using var ___ = admin;

        using (var response = await anonymous.UploadAsync(TestImages.Jpeg(600, 900, seed: 1), ct))
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
        }

        using (var response = await fan.UploadAsync(TestImages.Jpeg(600, 900, seed: 2), ct))
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden", ct);
        }

        api.MediaFiles().ShouldBeEmpty("upload negado gravou arquivo");

        using (var response = await artist.UploadAsync(TestImages.Jpeg(600, 900, seed: 3), ct))
        {
            await FlyerUploaded.ReadAsync(response, HttpStatusCode.Created, ct);
        }

        using (var response = await admin.UploadAsync(TestImages.Jpeg(600, 900, seed: 4), ct))
        {
            await FlyerUploaded.ReadAsync(response, HttpStatusCode.Created, ct);
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0022:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task WithoutTheCsrfHeaderTheUploadIsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (artist, _) = await api.ArtistAsync("artista.sem.csrf@subsolo.test", ct);
        using var _ = artist;

        using var response = await artist.UploadAsync(TestImages.Jpeg(600, 900), ct, withCsrf: false);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
        api.MediaFiles().ShouldBeEmpty("upload sem X-CSRF gravou arquivo");
    }

    [Fact]
    [Trait("Spec", "SPEC-0022:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task TheEleventhUploadInTheHourFromTheSameIpIsRateLimited()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct, productionRateLimits: true);
        var (artist, _) = await api.ArtistAsync("artista.insistente@subsolo.test", ct);
        using var _ = artist;
        var flyer = TestImages.Jpeg(400, 600);
        var token = await artist.CsrfTokenAsync(ct);

        for (var i = 0; i < 10; i++)
        {
            using var accepted = await artist.UploadAsync(flyer, ct, csrfToken: token);
            var body = await accepted.Content.ReadAsStringAsync(ct);
            accepted.StatusCode.ShouldBeOneOf(
                [HttpStatusCode.Created, HttpStatusCode.OK],
                $"envio {i + 1} de 10: {FlyersApiClient.Excerpt(body)}");
        }

        using var limited = await artist.UploadAsync(flyer, ct, csrfToken: token);

        await limited.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "security.rate_limited", ct);
    }
}
