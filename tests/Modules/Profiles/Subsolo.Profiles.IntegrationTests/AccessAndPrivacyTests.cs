using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Profiles.IntegrationEvents;
using Subsolo.Profiles.IntegrationTests.Infrastructure;

namespace Subsolo.Profiles.IntegrationTests;

/// <summary>
/// SPEC-0021 IT-04 — anônimo, fã e admin chamando as rotas de admin recebem 401 <c>auth.required</c>, 403
/// <c>auth.forbidden</c> e o resultado da rota (200 na fila); as mutações de admin sem <c>X-CSRF</c> dão 400
/// <c>security.csrf_invalid</c>. O contato e a prova aparecem só na fila do admin (com o e-mail de quem pediu), nunca em
/// <c>/me/profiles</c>, na busca nem no <see cref="IProfilesApi"/>. A fila com 200 pedidos pendentes (mais antigos
/// primeiro) responde em p95 ≤ 300 ms. O pedido tem o rate limit <c>report</c> (10 por hora por IP).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AccessAndPrivacyTests(PostgresFixture postgres)
{
    private const string ContactSentinel = "contato-sigiloso-41-99999-0042";
    private const string ProofSentinel = "prova-sigilosa-recibo-0042";
    private const int QueueSize = 200;
    private const int QueueCalls = 20;

    private static readonly TimeSpan QueueBudget = TimeSpan.FromMilliseconds(300);

    /// <summary>As rotas de admin do §6: método, rota (com <c>{id}</c> trocado por um id qualquer) e o tipo de corpo.</summary>
    public static readonly TheoryData<string, string, string> AdminEndpoints = new()
    {
        { "GET", "/api/v1/admin/profile-requests", "" },
        { "POST", "/api/v1/admin/profile-requests/{id}/approve", "" },
        { "POST", "/api/v1/admin/profile-requests/{id}/reject", "reason" },
        { "POST", "/api/v1/admin/profiles", "profile" },
    };

    /// <summary>As rotas de fã do §6.</summary>
    public static readonly TheoryData<string, string, string> FanEndpoints = new()
    {
        { "POST", "/api/v1/profiles/requests", "request" },
        { "GET", "/api/v1/profiles/search?q=pod", "" },
        { "POST", "/api/v1/profiles/{id}/claims", "proof" },
        { "GET", "/api/v1/me/profiles", "" },
    };

    private async Task<ProfilesApiFactory> FactoryAsync(CancellationToken ct, bool productionRateLimits = false) =>
        new(await ProfilesApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), productionRateLimits: productionRateLimits);

    private static string Route(string route) => route.Replace("{id}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal);

    private static object? Body(string kind) => kind switch
    {
        "reason" => new { reason = "Motivo qualquer da recusa." },
        "profile" => ProfilesRequests.Ownerless("Casa Proibida"),
        "request" => ProfilesRequests.Band("Banda Proibida"),
        "proof" => new { proof = "Sou eu mesmo." },
        _ => null,
    };

    [Theory]
    [Trait("Spec", "SPEC-0021:IT-04")]
    [Trait("Kind", "Integration")]
    [MemberData(nameof(AdminEndpoints))]
    public async Task AnonymousGets401OnAdminRoutes(string method, string route, string body)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        using var anonymous = api.CreateProfilesClient();

        using var response = await anonymous.SendAsync(
            new HttpMethod(method), Route(route), ct, Body(body), method == "GET" ? null : await anonymous.CsrfTokenAsync(ct));

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:IT-04")]
    [Trait("Kind", "Integration")]
    [MemberData(nameof(AdminEndpoints))]
    public async Task AFanGets403OnAdminRoutes(string method, string route, string body)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (fan, _) = await api.SignedInAsync("fa.curioso@subsolo.test", ct);
        using var _ = fan;

        using var response = await fan.SendAsync(
            new HttpMethod(method), Route(route), ct, Body(body), method == "GET" ? null : await fan.CsrfTokenAsync(ct));

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden", ct);
        (await ProfilesDatabase.ProfilesAsync(api.ConnectionString, ct)).ShouldBeEmpty("o fã não cria perfil pelo admin");
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:IT-04")]
    [Trait("Kind", "Integration")]
    [MemberData(nameof(FanEndpoints))]
    public async Task AnonymousGets401OnFanRoutes(string method, string route, string body)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        using var anonymous = api.CreateProfilesClient();

        using var response = await anonymous.SendAsync(
            new HttpMethod(method), Route(route), ct, Body(body), method == "GET" ? null : await anonymous.CsrfTokenAsync(ct));

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task TheAdminGetsTheQueueAndMutationsWithoutCsrfAre400()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.csrf@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        var requestId = await fan.RequestProfileAsync(ProfilesRequests.Band("Podridão"), ct);

        (await admin.QueueAsync(ct)).ById("requestId", requestId).ShouldNotBeNull();

        foreach (var (route, body) in new (string, object?)[]
        {
            (ProfilesRequests.Approve(requestId), null),
            (ProfilesRequests.Reject(requestId), new { reason = "Motivo qualquer da recusa." }),
            (ProfilesRequests.AdminProfiles, ProfilesRequests.Ownerless("Casa Sem Csrf")),
        })
        {
            using var response = await admin.SendAsync(HttpMethod.Post, route, ct, body);
            await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
        }

        (await ProfilesDatabase.RequestsAsync(api.ConnectionString, ct)).ShouldHaveSingleItem().Status.ShouldBe("pending", "nada mudou");
        (await ProfilesDatabase.ProfilesAsync(api.ConnectionString, ct)).ShouldBeEmpty("nada foi criado");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task ContactAndProofAppearOnlyInTheAdminQueue()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        const string requesterEmail = "fa.contato@subsolo.test";
        const string claimerEmail = "fa.prova@subsolo.test";
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        var (requester, requesterId) = await api.SignedInAsync(requesterEmail, ct);
        var (claimer, claimerId) = await api.SignedInAsync(claimerEmail, ct);
        using var _ = admin;
        using var __ = requester;
        using var ___ = claimer;
        var ownerless = await admin.CreateOwnerlessAsync(ProfilesRequests.Ownerless("Porão do Rock"), ct);
        var requestId = await requester.RequestProfileAsync(ProfilesRequests.Band("Podridão", ContactSentinel), ct);
        var claimId = await claimer.ClaimAsync(ownerless, ProofSentinel, ct);

        var queue = await admin.QueueAsync(ct);
        var request = queue.ById("requestId", requestId).ShouldNotBeNull(queue.ToString());
        request.GetProperty("kind").GetString().ShouldBe("new");
        request.GetProperty("type").GetString().ShouldBe("band");
        request.GetProperty("name").GetString().ShouldBe("Podridão");
        request.GetProperty("city").GetString().ShouldBe(ProfilesApiFactory.City);
        request.GetProperty("styles").EnumerateArray().Select(style => style.GetString()).ShouldBe([ProfilesApiFactory.Style]);
        request.GetProperty("links").GetProperty("instagram").GetString().ShouldBe("https://www.instagram.com/podridao");
        request.GetProperty("contactOrProof").GetString().ShouldBe(ContactSentinel);
        request.GetProperty("requesterEmail").GetString().ShouldBe(requesterEmail);
        request.TryGetProperty("createdAtUtc", out var createdAt).ShouldBeTrue();
        createdAt.ValueKind.ShouldBe(JsonValueKind.String);
        var claim = queue.ById("requestId", claimId).ShouldNotBeNull(queue.ToString());
        claim.GetProperty("kind").GetString().ShouldBe("claim");
        claim.GetProperty("contactOrProof").GetString().ShouldBe(ProofSentinel);
        claim.GetProperty("requesterEmail").GetString().ShouldBe(claimerEmail);
        claim.GetProperty("profileId").GetGuid().ShouldBe(ownerless);
        claim.GetProperty("name").GetString().ShouldBe("Porão do Rock");

        var outside = new List<(string Where, string Body)>
        {
            ("/me/profiles do autor", (await requester.MyProfilesAsync(ct)).ToString()),
            ("/me/profiles de quem reivindicou", (await claimer.MyProfilesAsync(ct)).ToString()),
            ("busca", (await requester.SearchAsync("por", ct)).ToString()),
        };
        var approved = await admin.ApproveAsync(requestId, ct);
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var profiles = scope.ServiceProvider.GetRequiredService<IProfilesApi>();
            outside.Add(("IProfilesApi.GetOwnedProfilesAsync", JsonSerializer.Serialize(await profiles.GetOwnedProfilesAsync(requesterId, ct))));
            outside.Add(("IProfilesApi.GetAsync", JsonSerializer.Serialize(await profiles.GetAsync(approved, ct))));
            outside.Add(("IProfilesApi.GetAsync (sem dono)", JsonSerializer.Serialize(await profiles.GetAsync(ownerless, ct))));
            (await profiles.GetOwnedProfilesAsync(requesterId, ct)).ShouldHaveSingleItem().Id.ShouldBe(approved);
            (await profiles.GetOwnedProfilesAsync(claimerId, ct)).ShouldBeEmpty("a reivindicação ainda não foi aprovada");
            (await profiles.GetAsync(Guid.CreateVersion7(), ct)).ShouldBeNull();
        }

        foreach (var (where, body) in outside)
        {
            body.ShouldNotContain(ContactSentinel, Case.Insensitive, $"o contato apareceu em {where}");
            body.ShouldNotContain(ProofSentinel, Case.Insensitive, $"a prova apareceu em {where}");
            body.ShouldNotContain(requesterEmail, Case.Insensitive, $"o e-mail apareceu em {where}");
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task TheQueueWith200PendingRequestsIsOldestFirstAndUnder300MillisecondsAtP95()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        for (var i = 0; i < QueueSize; i++)
        {
            var (fan, _) = await api.SignedInAsync(string.Create(CultureInfo.InvariantCulture, $"fila-{i:000}@subsolo.test"), ct);
            using (fan)
            {
                await fan.RequestProfileAsync(ProfilesRequests.Band(string.Create(CultureInfo.InvariantCulture, $"Banda da Fila {i:000}")), ct);
            }
        }

        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        using var _ = admin;
        var durations = new List<TimeSpan>();
        JsonElement queue = default;
        for (var i = 0; i <= QueueCalls; i++)
        {
            var (response, elapsed) = await Timing.TimedAsync(() => admin.GetAsync(ProfilesRequests.AdminQueue, ct));
            using (response)
            {
                queue = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
            }

            if (i > 0)
            {
                durations.Add(elapsed);
            }
        }

        Timing.Percentile(durations, 95).ShouldBeLessThanOrEqualTo(
            QueueBudget, $"p95 da fila com {QueueSize} pedidos (ms): {string.Join(", ", durations.Select(d => (int)d.TotalMilliseconds))}");
        queue.GetArrayLength().ShouldBe(QueueSize);
        var names = queue.EnumerateArray().Select(item => item.GetProperty("name").GetString()).ToList();
        names[0].ShouldBe("Banda da Fila 000", "mais antigos primeiro");
        names[^1].ShouldBe("Banda da Fila 199", "mais antigos primeiro");
        var created = queue.EnumerateArray().Select(item => item.GetProperty("createdAtUtc").GetDateTimeOffset()).ToList();
        created.ShouldBe(created.Order().ToList(), "a fila vem do mais antigo para o mais novo");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task TheRequestHasTheReportRateLimitOf10PerHour()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct, productionRateLimits: true);
        var (fan, _) = await api.SignedInAsync("fa.insistente@subsolo.test", ct);
        using var _ = fan;
        var token = await fan.CsrfTokenAsync(ct);
        for (var i = 0; i < 10; i++)
        {
            using var accepted = await fan.SendAsync(HttpMethod.Post, ProfilesRequests.Requests, ct, ProfilesRequests.Band("Podridão"), token);
            accepted.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests, $"pedido {i + 1} de 10 barrado pelo limite");
        }

        using var limited = await fan.SendAsync(HttpMethod.Post, ProfilesRequests.Requests, ct, ProfilesRequests.Band("Podridão"), token);

        await limited.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "security.rate_limited", ct);
    }
}
