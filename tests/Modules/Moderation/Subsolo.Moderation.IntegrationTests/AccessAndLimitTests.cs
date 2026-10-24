using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Moderation.IntegrationTests.Infrastructure;

namespace Subsolo.Moderation.IntegrationTests;

/// <summary>
/// SPEC-0033 IT-02 — com os limites de produção: o fã que envia 11 denúncias de shows diferentes no mesmo dia recebe 429
/// <c>security.rate_limited</c> na 11ª (política <c>moderation-report</c>, 10 por fã em 24 h, Emenda 1), e outro fã no
/// mesmo IP continua denunciando (a chave é o usuário, não o IP). Anônimo denunciando recebe 401 <c>auth.required</c>;
/// a fila e as decisões dão 401 ao anônimo e 403 <c>auth.forbidden</c> ao fã; denúncia e decisões sem <c>X-CSRF</c> dão
/// 400 <c>security.csrf_invalid</c>; a fila sai com <c>no-store</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AccessAndLimitTests(PostgresFixture postgres)
{
    private const int DailyLimit = 10;

    /// <summary>As rotas de admin do §6: método, rota (com <c>{id}</c> trocado por um id qualquer) e se leva corpo.</summary>
    public static readonly TheoryData<string, string> AdminEndpoints = new()
    {
        { "GET", ModerationRequests.AdminReports },
        { "POST", "/api/v1/admin/reports/{id}/keep" },
        { "POST", "/api/v1/admin/reports/{id}/hide" },
    };

    private async Task<ModerationApiFactory> FactoryAsync(CancellationToken ct, bool productionRateLimits = false) =>
        new(await ModerationApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), productionRateLimits: productionRateLimits);

    private static string Route(string route) => route.Replace("{id}", Guid.CreateVersion7().ToString(), StringComparison.Ordinal);

    private static object? Body(string route) => route.EndsWith("/hide", StringComparison.Ordinal) ? new { note = (string?)null } : null;

    [Fact]
    [Trait("Spec", "SPEC-0033:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task The11thReportOfTheDayIs429AndAnotherFanStillReports()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct, productionRateLimits: true);
        var (admin, _) = await api.SignedInAsync(ModerationApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.insistente@subsolo.test", ct);
        var (other, _) = await api.SignedInAsync("outro.fa.mesmo.ip@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        using var ___ = other;
        var template = await admin.CreateShowAsync("Noite em Série", ct);
        var shows = new List<Guid> { template.Id };
        shows.AddRange(await ModerationDatabase.CloneShowAsync(api.ConnectionString, template.Id, DailyLimit, ct));

        for (var i = 0; i < DailyLimit; i++)
        {
            using var accepted = await fan.PostAsync(ModerationRequests.Reports(shows[i]), ModerationRequests.ReportBody("fake"), ct);
            accepted.StatusCode.ShouldBe(
                HttpStatusCode.Created,
                $"denúncia {i + 1} de {DailyLimit}: {ModerationApiClient.Excerpt(await accepted.Content.ReadAsStringAsync(ct))}");
        }

        using (var limited = await fan.PostAsync(ModerationRequests.Reports(shows[DailyLimit]), ModerationRequests.ReportBody("fake"), ct))
        {
            await limited.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "security.rate_limited", ct);
        }

        (await ModerationDatabase.ReportsAsync(api.ConnectionString, ct)).Count.ShouldBe(DailyLimit, "a 11ª denúncia foi gravada");
        await other.ReportAsync(shows[DailyLimit], "fake", ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0033:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task AnonymousGets401WhenReporting()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        using var anonymous = api.CreateModerationClient();

        using var response = await anonymous.PostAsync(
            ModerationRequests.Reports(Guid.CreateVersion7()), ModerationRequests.ReportBody("fake"), ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
    }

    [Theory]
    [Trait("Spec", "SPEC-0033:IT-02")]
    [Trait("Kind", "Integration")]
    [MemberData(nameof(AdminEndpoints))]
    public async Task AnonymousGets401OnTheQueueAndTheDecisions(string method, string route)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        using var anonymous = api.CreateModerationClient();

        using var response = await anonymous.SendAsync(
            new HttpMethod(method), Route(route), ct, Body(route), method == "GET" ? null : await anonymous.CsrfTokenAsync(ct));

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
    }

    [Theory]
    [Trait("Spec", "SPEC-0033:IT-02")]
    [Trait("Kind", "Integration")]
    [MemberData(nameof(AdminEndpoints))]
    public async Task AFanGets403OnTheQueueAndTheDecisions(string method, string route)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (fan, _) = await api.SignedInAsync("fa.curioso@subsolo.test", ct);
        using var _ = fan;

        using var response = await fan.SendAsync(
            new HttpMethod(method), Route(route), ct, Body(route), method == "GET" ? null : await fan.CsrfTokenAsync(ct));

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden", ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0033:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task MutationsWithoutCsrfAre400AndTheQueueIsNoStore()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ModerationApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.sem.csrf@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        var show = await admin.CreateShowAsync("Noite Sem Token", ct);

        using (var report = await fan.SendAsync(HttpMethod.Post, ModerationRequests.Reports(show.Id), ct, ModerationRequests.ReportBody("fake")))
        {
            await report.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
        }

        await fan.ReportAsync(show.Id, "fake", ct);
        foreach (var (route, body) in new (string, object?)[]
                 {
                     (ModerationRequests.Keep(show.Id), null),
                     (ModerationRequests.Hide(show.Id), new { note = (string?)null }),
                 })
        {
            using var response = await admin.SendAsync(HttpMethod.Post, route, ct, body);
            await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
        }

        (await ModerationDatabase.ReportsAsync(api.ConnectionString, ct)).ShouldHaveSingleItem().Status.ShouldBe("pending", "nada foi decidido");
        (await admin.QueueAsync(ct)).GetArrayLength().ShouldBe(1);
    }
}
