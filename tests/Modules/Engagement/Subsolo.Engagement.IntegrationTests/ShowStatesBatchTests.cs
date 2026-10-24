using Microsoft.Extensions.DependencyInjection;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0032 H29-01 (contrato v2) — <c>IEventsCatalog.GetShowStatesAsync(ids)</c> do host, sobre o PostgreSQL real: com um
/// show agendado e um cancelado cadastrados pela API e um id que não existe, devolve a situação e o início só dos dois
/// encontrados (ids repetidos contam uma vez); com a lista vazia, nada. E "meus vou" (<c>GET /me/attendances</c>) continua
/// listando o agendado e o cancelado futuros.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ShowStatesBatchTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0032:H29-01")]
    [Trait("Kind", "Integration")]
    public async Task TheCatalogReturnsTheStatesOfTheFoundShowsOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new EngagementApiFactory(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync($"fa.lote-{Guid.NewGuid():N}@subsolo.test", ct);
        using var a = admin;
        using var f = fan;
        var now = DateTimeOffset.UtcNow;
        var scheduled = await admin.CreateReminderShowAsync("Noite em Lote", now, days: 4, ct);
        var cancelled = await admin.CreateReminderShowAsync("Noite em Lote Cancelada", now, days: 5, ct);
        await fan.AttendAsync(scheduled.Id, ct);
        await fan.AttendAsync(cancelled.Id, ct);
        await admin.CancelShowAsync(cancelled.Id, ct);
        var missing = Guid.CreateVersion7();

        var states = await StatesAsync(api, [scheduled.Id, cancelled.Id, missing, scheduled.Id], ct);
        var none = await StatesAsync(api, [], ct);

        states.Keys.ShouldBe([scheduled.Id, cancelled.Id], ignoreOrder: true, "só os shows encontrados");
        states[scheduled.Id].ShouldBe(new ShowState("scheduled", ReminderRequests.LocalToUtc(scheduled.StartsLocal)));
        states[cancelled.Id].ShouldBe(new ShowState("cancelled", ReminderRequests.LocalToUtc(cancelled.StartsLocal)));
        none.ShouldBeEmpty();
        (await fan.UpcomingAsync(ct)).ShouldBe([scheduled.Id, cancelled.Id], "\"meus vou\" mudou com a leitura em lote");
    }

    private static async Task<IReadOnlyDictionary<Guid, ShowState>> StatesAsync(
        EngagementApiFactory api, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IEventsCatalog>().GetShowStatesAsync(ids, ct);
    }
}
