using System.Net;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0027 IT-02 — quatro <c>PUT</c> iguais do mesmo fã mandados ao mesmo tempo (cada um com o próprio token
/// anti-forgery, liberados juntos por um semáforo, em várias rodadas): todos 204, nunca 409 nem 500, e um follow só no
/// banco (a chave primária <c>(user_id, target_kind, target_key)</c> decide a corrida).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class FollowRaceTests(PostgresFixture postgres)
{
    private const int Parallel = 4;

    private const int Rounds = 5;

    [Fact]
    [Trait("Spec", "SPEC-0027:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task FourParallelPutsOfTheSameCityAreAll204AndLeaveOneFollow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new EngagementApiFactory(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (fan, fanId) = await api.SignedInAsync("fa.corrida@subsolo.test", ct);
        using var _ = fan;

        for (var round = 0; round < Rounds; round++)
        {
            await RaceAsync(fan, EngagementRequests.FollowCity(EngagementApiFactory.City), ct);
            (await EngagementDatabase.FollowsOfAsync(api.ConnectionString, fanId, ct))
                .ShouldHaveSingleItem($"rodada {round}: a corrida deixou mais de um follow (ou nenhum)");
            await fan.DeleteAsync(EngagementRequests.FollowCity(EngagementApiFactory.City), ct).ShouldSucceedAsync(ct);
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task FourParallelPutsOfTheSameProfileAreAll204AndLeaveOneFollow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new EngagementApiFactory(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, fanId) = await api.SignedInAsync("fa.corrida.perfil@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        var bandId = await admin.CreateApprovedProfileAsync("Podridão", ct);

        for (var round = 0; round < Rounds; round++)
        {
            await RaceAsync(fan, EngagementRequests.FollowProfile(bandId), ct);
            (await EngagementDatabase.FollowsOfAsync(api.ConnectionString, fanId, ct))
                .ShouldHaveSingleItem($"rodada {round}: a corrida deixou mais de um follow (ou nenhum)");
            await fan.DeleteAsync(EngagementRequests.FollowProfile(bandId), ct).ShouldSucceedAsync(ct);
        }
    }

    /// <summary>Manda <see cref="Parallel"/> PUTs iguais ao mesmo tempo e exige 204 em todos.</summary>
    private static async Task RaceAsync(EngagementApiClient fan, string route, CancellationToken ct)
    {
        var tokens = new List<string>();
        for (var i = 0; i < Parallel; i++)
        {
            tokens.Add(await fan.CsrfTokenAsync(ct));
        }

        using var start = new SemaphoreSlim(0, Parallel);
        var sends = tokens.Select(async token =>
        {
            await start.WaitAsync(ct);
            return await fan.SendAsync(HttpMethod.Put, route, ct, csrfToken: token);
        }).ToList();
        start.Release(Parallel);
        var responses = await Task.WhenAll(sends);
        try
        {
            var statuses = responses.Select(response => (int)response.StatusCode).ToList();
            statuses.ShouldAllBe(
                status => status == (int)HttpStatusCode.NoContent,
                $"PUTs simultâneos de {route}: {string.Join(", ", statuses)}");
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }
}
