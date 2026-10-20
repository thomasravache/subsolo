using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Site.IntegrationTests.Infrastructure;

namespace Subsolo.Site.IntegrationTests;

/// <summary>
/// SPEC-0028 IT-01 — com a API e o PostgreSQL, o admin cadastra dois apoiadores (um com link <c>https</c>, outro sem) e
/// <c>GET /api/v1/supporters</c> (anônimo) traz os dois, exatamente <c>{id, name, url|null}</c>, na ordem de cadastro,
/// com p95 ≤ 100 ms (20 leituras depois de 3 de aquecimento); sem apoiadores, a lista vem vazia. E a parte "aparece e
/// some" do admin (IT-02): o removido sai da lista pública.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class SupportersApiTests(PostgresFixture postgres)
{
    private const int Warmup = 3;

    private const int Samples = 20;

    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(100);

    [Fact]
    [Trait("Spec", "SPEC-0028:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task WithoutSupportersTheListIsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new SiteApiFactory(await SiteApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        using var anonymous = api.CreateSiteClient();

        (await anonymous.SupportersAsync(ct)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0028:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task TwoSupportersComeInRegistrationOrderWithinBudget()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new SiteApiFactory(await SiteApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        using var admin = await api.SignedInAsync(SiteApiFactory.AdminEmail, ct);
        using var anonymous = api.CreateSiteClient();

        // Ordem de cadastro, não alfabética: "Zine" antes de "Abismo".
        var first = await admin.AddSupporterAsync("Putridearth Zine", "https://zine.example", ct);
        var second = await admin.AddSupporterAsync("Abismo Records", null, ct);

        (await anonymous.SupportersAsync(ct)).ShouldBe(
        [
            new SupporterItem(first, "Putridearth Zine", "https://zine.example"),
            new SupporterItem(second, "Abismo Records", null),
        ]);

        var elapsed = new List<TimeSpan>();
        for (var i = 0; i < Warmup + Samples; i++)
        {
            var (response, time) = await Timing.TimedAsync(() => anonymous.GetAsync(SiteRequests.Supporters, ct));
            using (response)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            if (i >= Warmup)
            {
                elapsed.Add(time);
            }
        }

        var p95 = Timing.Percentile(elapsed, 95);
        p95.ShouldBeLessThanOrEqualTo(
            Budget,
            $"GET {SiteRequests.Supporters} p95 = {p95.TotalMilliseconds:F0} ms (amostras: {string.Join(", ", elapsed.Select(time => $"{time.TotalMilliseconds:F0}"))})");
    }

    [Fact]
    [Trait("Spec", "SPEC-0028:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task TheRemovedSupporterLeavesThePublicList()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new SiteApiFactory(await SiteApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        using var admin = await api.SignedInAsync(SiteApiFactory.AdminEmail, ct);
        using var anonymous = api.CreateSiteClient();
        var kept = await admin.AddSupporterAsync("Putridearth Zine", null, ct);
        var removed = await admin.AddSupporterAsync("Abismo Records", "https://abismo.example", ct);

        using (var response = await admin.DeleteAsync(SiteRequests.AdminSupporter(removed), ct))
        {
            await response.ShouldBeNoContentAsync(ct);
        }

        (await anonymous.SupportersAsync(ct)).Select(item => item.Id).ShouldBe([kept]);
    }
}
