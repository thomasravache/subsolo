using System.Net;
using Subsolo.Events.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Events.IntegrationTests;

/// <summary>
/// SPEC-0029 CH-01 (caracterização) — guarda do contrato da SPEC-0008: dada a agenda com shows (um agendado e um
/// cancelado, cadastrados pelo admin com um produtor sem dono), quando lida (<c>GET /api/v1/cities/{city}/agenda</c>),
/// então <c>attendingCount</c> vem 0 em todos os shows. Passa antes da SPEC-0029 e continua passando depois: a contagem
/// de "vão" vem do <c>Engagement</c> pelo web (§4), nunca do <c>Events</c> (sem ciclo, ADR-0001).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AttendingCountCharacterizationTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0029:CH-01")]
    [Trait("Kind", "Integration")]
    public async Task TheAgendaAlwaysHasAttendingCountZeroFromTheEventsModule()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new FlyersApiFactory(await FlyersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(FlyersApiFactory.AdminEmail, ct);
        using var _ = admin;
        var producer = await admin.OwnerlessAsync("producer", "Peste Produções", ct);
        var flyerId = await admin.FlyerAsync(ct);
        var scheduled = await admin.CreateShowAsync(
            ShowsApi.ShowBody(producer, flyerId, ShowsApi.LocalAt(DateTimeOffset.UtcNow, days: 5)), ct);
        var cancelled = await admin.CreateShowAsync(
            ShowsApi.ShowBody(producer, flyerId, ShowsApi.LocalAt(DateTimeOffset.UtcNow, days: 6), title: "Sepulcro Fest"), ct);
        using (var cancel = await admin.PostJsonAsync(ShowsApi.Status(cancelled.Id), new { status = "cancelled" }, ct))
        {
            cancel.StatusCode.ShouldBe(HttpStatusCode.OK, FlyersApiClient.Excerpt(await cancel.Content.ReadAsStringAsync(ct)));
        }

        var shows = await admin.AgendaShowsAsync(ShowsApi.City, ct);

        foreach (var id in new[] { scheduled.Id, cancelled.Id })
        {
            var show = shows.ById(id).ShouldNotBeNull($"o show {id} não está na agenda: {shows}");
            show.GetProperty("attendingCount").GetInt32().ShouldBe(0, $"attendingCount do Events: {show}");
        }

        shows.EnumerateArray().Select(show => show.GetProperty("attendingCount").GetInt32()).ShouldAllBe(count => count == 0);
    }
}
