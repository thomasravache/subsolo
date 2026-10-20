using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Engagement.IntegrationEvents;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0029 IT-03 — dados 50 shows com contagens variadas (0 a 49 "vou" cada, gravados direto em
/// <c>engagement.attendances</c>), <c>GET /api/v1/attendances/counts?showIds=…</c> (público) devolve
/// <c>{counts: {"&lt;showId&gt;": n}}</c> com os números certos (show sem "vou" conta 0 ou fica de fora) e o p95 de 20
/// chamadas (depois de 3 de aquecimento) fica em ≤ 50 ms; até 100 ids → 200; 101 ids ou id inválido → 400
/// <c>engagement.invalid_query</c>. O <see cref="IAttendanceApi"/> do contêiner do host dá as mesmas contagens e os ids
/// de quem marcou um show (base do lembrete).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AttendanceCountsTests(PostgresFixture postgres)
{
    private const int Shows = 50;

    private const int Warmup = 3;

    private const int Samples = 20;

    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(50);

    private async Task<EngagementApiFactory> StartedFactoryAsync(CancellationToken ct)
    {
        var api = new EngagementApiFactory(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        // Sobe o host (aplica as migrações, com a tabela engagement.attendances) antes da carga direta.
        using var warm = api.CreateEngagementClient();
        using var _ = await warm.GetAsync(EngagementRequests.CityFollowers(EngagementApiFactory.City), ct);
        return api;
    }

    /// <summary>50 shows; o show i tem i "vou" (de usuários novos), e o primeiro fã de cada show marcou todos.</summary>
    private static async Task<(IReadOnlyList<Guid> ShowIds, Dictionary<Guid, List<Guid>> Attendees)> SeedAsync(
        EngagementApiFactory api, CancellationToken ct)
    {
        var regular = Guid.CreateVersion7();
        var showIds = Enumerable.Range(0, Shows).Select(_ => Guid.CreateVersion7()).ToList();
        var attendees = new Dictionary<Guid, List<Guid>>();
        for (var i = 0; i < Shows; i++)
        {
            List<Guid> users = i == 0 ? [] : Enumerable.Range(0, i - 1).Select(_ => Guid.CreateVersion7()).Prepend(regular).ToList();
            attendees[showIds[i]] = users;
            if (users.Count > 0)
            {
                await AttendanceDatabase.SeedAsync(api.ConnectionString, showIds[i], users, ct);
            }
        }

        // Ruído: "vou" em shows que não são pedidos.
        await AttendanceDatabase.SeedAsync(api.ConnectionString, Guid.CreateVersion7(), [regular, Guid.CreateVersion7()], ct);
        await AttendanceDatabase.AnalyzeAsync(api.ConnectionString, ct);
        return (showIds, attendees);
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task CountsOfFiftyShowsAreRightAndWithinBudget()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await StartedFactoryAsync(ct);
        var (showIds, attendees) = await SeedAsync(api, ct);
        using var anonymous = api.CreateEngagementClient();
        var route = AttendanceRequests.Counts(showIds);

        var elapsed = new List<TimeSpan>();
        IReadOnlyDictionary<Guid, int> counts = new Dictionary<Guid, int>();
        for (var i = 0; i < Warmup + Samples; i++)
        {
            var (response, time) = await Timing.TimedAsync(() => anonymous.GetAsync(route, ct));
            using (response)
            {
                counts = AttendanceRequests.ReadCounts(await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct));
            }

            if (i >= Warmup)
            {
                elapsed.Add(time);
            }
        }

        foreach (var showId in showIds)
        {
            counts.GetValueOrDefault(showId).ShouldBe(attendees[showId].Count, $"contagem do show {showId}");
        }

        counts.Keys.ShouldBeSubsetOf(showIds, "a resposta trouxe show que não foi pedido");
        var p95 = Timing.Percentile(elapsed, 95);
        p95.ShouldBeLessThanOrEqualTo(
            Budget,
            $"GET /attendances/counts p95 = {p95.TotalMilliseconds:F0} ms (amostras: {string.Join(", ", elapsed.Select(time => $"{time.TotalMilliseconds:F0}"))})");
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task TheAttendanceApiGivesTheSameCountsAndTheAttendees()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await StartedFactoryAsync(ct);
        var (showIds, attendees) = await SeedAsync(api, ct);

        await using var scope = api.Services.CreateAsyncScope();
        var attendance = scope.ServiceProvider.GetRequiredService<IAttendanceApi>();
        var counts = await attendance.CountAsync(showIds, ct);
        var busiest = showIds[^1];
        var who = await attendance.GetAttendeesAsync(busiest, ct);
        var nobody = await attendance.GetAttendeesAsync(showIds[0], ct);

        foreach (var showId in showIds)
        {
            counts.GetValueOrDefault(showId).ShouldBe(attendees[showId].Count, $"CountAsync do show {showId}");
        }

        who.ShouldBe(attendees[busiest], ignoreOrder: true);
        who.Count.ShouldBe(attendees[busiest].Count, "ids repetidos");
        nobody.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task UpToOneHundredIdsIs200AndMoreIs400()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await StartedFactoryAsync(ct);
        using var anonymous = api.CreateEngagementClient();
        var ids = Enumerable.Range(0, 101).Select(_ => Guid.CreateVersion7()).ToList();

        using (var hundred = await anonymous.GetAsync(AttendanceRequests.Counts(ids.Take(100)), ct))
        {
            var body = await hundred.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
            AttendanceRequests.ReadCounts(body).Values.ShouldAllBe(count => count == 0);
        }

        using var tooMany = await anonymous.GetAsync(AttendanceRequests.Counts(ids), ct);
        await tooMany.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "engagement.invalid_query", ct);
    }

    [Theory]
    [Trait("Spec", "SPEC-0029:IT-03")]
    [Trait("Kind", "Integration")]
    [InlineData("nao-e-um-id")]
    [InlineData("123")]
    public async Task AnInvalidIdIs400(string invalid)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await StartedFactoryAsync(ct);
        using var anonymous = api.CreateEngagementClient();

        using var response = await anonymous.GetAsync(
            $"{AttendanceRequests.CountsRoute}?showIds={Guid.CreateVersion7()},{invalid}", ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "engagement.invalid_query", ct);
    }
}
