using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Engagement.IntegrationEvents;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0027 IT-04 — dados 10.000 seguidores da cidade e 500 de um perfil (250 deles também seguem a cidade) gravados
/// direto em <c>engagement.follows</c>, <see cref="IEngagementApi.GetFollowersAsync"/> (resolvido do contêiner do host,
/// como a spec de avisos fará) devolve os 10.250 ids únicos, e o p95 de 20 chamadas (depois de 3 de aquecimento) fica
/// em ≤ 200 ms. SPEC-0029 (H27-09): o perfil é um perfil aprovado de verdade (criado pelo admin), porque follows de
/// perfil que não existe mais não contam.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class FollowersPerformanceTests(PostgresFixture postgres)
{
    private const int CityFollowers = 10_000;

    private const int ProfileFollowers = 500;

    private const int Overlap = 250;

    private const int Warmup = 3;

    private const int Samples = 20;

    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(200);

    [Fact]
    [Trait("Spec", "SPEC-0027:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task GetFollowersReturnsTheUniqueIdsOfTenThousandFollowersWithinBudget()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new EngagementApiFactory(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        // Sobe o host (aplica as migrações, com o schema engagement) antes da carga direta.
        using (var warm = api.CreateEngagementClient())
        {
            using var health = await warm.GetAsync(EngagementRequests.CityFollowers(EngagementApiFactory.City), ct);
        }

        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        Guid profileId;
        using (admin)
        {
            profileId = await admin.CreateApprovedProfileAsync("Podridão", ct);
        }

        var cityFans = Enumerable.Range(0, CityFollowers).Select(_ => Guid.CreateVersion7()).ToList();
        var profileFans = cityFans.Take(Overlap)
            .Concat(Enumerable.Range(0, ProfileFollowers - Overlap).Select(_ => Guid.CreateVersion7()))
            .ToList();
        await EngagementDatabase.SeedAsync(api.ConnectionString, cityFans, "city", EngagementApiFactory.City, ct);
        await EngagementDatabase.SeedAsync(api.ConnectionString, profileFans, "profile", profileId.ToString("D"), ct);
        await EngagementDatabase.AnalyzeAsync(api.ConnectionString, ct);
        var expected = cityFans.Concat(profileFans).ToHashSet();
        expected.Count.ShouldBe(CityFollowers + ProfileFollowers - Overlap);

        var elapsed = new List<TimeSpan>();
        IReadOnlyCollection<Guid> followers = [];
        for (var i = 0; i < Warmup + Samples; i++)
        {
            await using var scope = api.Services.CreateAsyncScope();
            var engagement = scope.ServiceProvider.GetRequiredService<IEngagementApi>();
            var watch = Stopwatch.StartNew();
            followers = await engagement.GetFollowersAsync(EngagementApiFactory.City, [profileId, Guid.CreateVersion7()], ct);
            watch.Stop();
            if (i >= Warmup)
            {
                elapsed.Add(watch.Elapsed);
            }
        }

        followers.Count.ShouldBe(expected.Count, "ids repetidos ou faltando");
        followers.ToHashSet().SetEquals(expected).ShouldBeTrue("os ids devolvidos não são os de quem segue a cidade ou o perfil");
        var p95 = Timing.Percentile(elapsed, 95);
        p95.ShouldBeLessThanOrEqualTo(
            Budget,
            $"GetFollowersAsync p95 = {p95.TotalMilliseconds:F0} ms (amostras: {string.Join(", ", elapsed.Select(time => $"{time.TotalMilliseconds:F0}"))})");
    }
}
