using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.IntegrationTests.Common;
using Subsolo.Moderation.IntegrationTests.Infrastructure;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Moderation.IntegrationTests;

/// <summary>
/// SPEC-0037 — os ajustes da fila de moderação (achados da revisão G4 da SPEC-0033):
/// <list type="bullet">
/// <item>IT-01 (H33-01) — três denúncias simultâneas do mesmo fã para o mesmo show: uma 201, as outras 409
/// <c>moderation.already_reported</c> e uma linha só (índice único parcial, Emenda 1 da SPEC-0033); <c>/hide</c> com nota
/// de 201 caracteres dá 400 <c>moderation.invalid_note</c> (Emenda 4) e não decide nada;</item>
/// <item>IT-02 (H33-05) — show oculto com uma denúncia <c>pending</c> que chegou depois da decisão: denunciar o show oculto
/// dá 404 <c>events.show_not_found</c>; uma decisão nova (ocultar) vira a pendente <c>hidden</c>;</item>
/// <item>IT-02 (H33-06) — o <c>IShowHeadlines</c> devolve o fuso da cidade do show (<c>ShowHeadline.TimeZone</c>).</item>
/// </list>
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class QueueAdjustmentsApiTests(PostgresFixture postgres)
{
    /// <summary>Prazo para a outbox do Moderation e a inbox do Events (a cada 2 s) entregarem o evento.</summary>
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);

    private async Task<ModerationApiFactory> FactoryAsync(CancellationToken ct) =>
        new(await ModerationApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

    [Fact]
    [Trait("Spec", "SPEC-0037:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task ThreeSimultaneousReportsOfTheSameFanGiveOne201AndTwo409()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ModerationApiFactory.AdminEmail, ct);
        using var _ = admin;
        var show = await admin.CreateShowAsync("Noite da Corrida", ct);

        // Três sessões do mesmo fã (cada uma com o próprio pote de cookies), com o X-CSRF já em mãos.
        const string email = "fa.corrida@subsolo.test";
        var sessions = new List<(ModerationApiClient Client, string Token)>();
        var ids = new HashSet<Guid>();
        try
        {
            for (var index = 0; index < 3; index++)
            {
                var (client, userId) = await api.SignedInAsync(email, ct);
                sessions.Add((client, await client.CsrfTokenAsync(ct)));
                ids.Add(userId);
            }

            ids.ShouldHaveSingleItem("as três sessões não são do mesmo fã");
            using var start = new Barrier(sessions.Count);
            var responses = await Task.WhenAll(sessions.Select(session => Task.Run(
                async () =>
                {
                    start.SignalAndWait(ct);
                    return await session.Client.SendAsync(
                        HttpMethod.Post, ModerationRequests.Reports(show.Id), ct, ModerationRequests.ReportBody("fake"), session.Token);
                },
                ct)));

            try
            {
                responses.Count(response => response.StatusCode == HttpStatusCode.Created)
                    .ShouldBe(1, $"status: {string.Join(", ", responses.Select(response => (int)response.StatusCode))}");
                foreach (var conflict in responses.Where(response => response.StatusCode != HttpStatusCode.Created))
                {
                    await conflict.ShouldBeProblemAsync(HttpStatusCode.Conflict, "moderation.already_reported", ct);
                }
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }
            }
        }
        finally
        {
            foreach (var session in sessions)
            {
                session.Client.Dispose();
            }
        }

        (await ModerationDatabase.ReportsAsync(api.ConnectionString, ct))
            .ShouldHaveSingleItem("as denúncias simultâneas gravaram mais (ou menos) de uma linha")
            .ShowId.ShouldBe(show.Id);
    }

    [Fact]
    [Trait("Spec", "SPEC-0037:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task HidingWithA201CharacterNoteIs400InvalidNote()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ModerationApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.nota.longa@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        var show = await admin.CreateShowAsync("Noite da Nota Longa", ct);
        await fan.ReportAsync(show.Id, "fake", ct);

        using var response = await admin.PostAsync(ModerationRequests.Hide(show.Id), new { note = new string('n', 201) }, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "moderation.invalid_note", ct);
        (await ModerationDatabase.ReportsAsync(api.ConnectionString, ct))
            .ShouldHaveSingleItem().Status.ShouldBe("pending", "a nota inválida decidiu a denúncia");
        (await ModerationDatabase.OutboxContentsAsync(api.ConnectionString, "ShowHiddenByModeration", ct))
            .ShouldBeEmpty("a nota inválida ocultou o show");
    }

    [Fact]
    [Trait("Spec", "SPEC-0037:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task ALateReportOfAHiddenShowIsResolvedByANewDecision()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, adminId) = await api.SignedInAsync(ModerationApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.primeira.denuncia@subsolo.test", ct);
        var (late, lateId) = await api.SignedInAsync("fa.denuncia.tardia@subsolo.test", ct);
        var (after, _) = await api.SignedInAsync("fa.depois.de.oculto@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        using var ___ = late;
        using var ____ = after;
        var show = await admin.CreateShowAsync("Noite Tardia", ct);
        await fan.ReportAsync(show.Id, "fake", ct);
        await admin.HideAsync(show.Id, null, ct);
        (await Timing.EventuallyAsync(
                async () => await ModerationDatabase.HiddenAtAsync(api.ConnectionString, show.Id, ct) is not null,
                DeliveryTimeout,
                ct))
            .ShouldBeTrue("o Events não marcou o show como oculto (hidden_at_utc)");

        // A denúncia aceita entre a decisão e o consumidor do Events (ainda pendente).
        var lateReport = await ModerationDatabase.InsertPendingReportAsync(api.ConnectionString, show.Id, lateId, "wrong_info", ct);

        // Denunciar o show já oculto: 404, nada gravado.
        using (var report = await after.PostAsync(ModerationRequests.Reports(show.Id), ModerationRequests.ReportBody("fake"), ct))
        {
            await report.ShouldBeProblemAsync(HttpStatusCode.NotFound, "events.show_not_found", ct);
        }

        (await ModerationDatabase.ReportsAsync(api.ConnectionString, ct)).Count.ShouldBe(2, "a denúncia do show oculto foi gravada");

        // Uma decisão nova sobre o mesmo show resolve a pendente tardia.
        await admin.HideAsync(show.Id, null, ct);

        var row = (await ModerationDatabase.ReportsAsync(api.ConnectionString, ct)).Single(report => report.Id == lateReport);
        row.Status.ShouldBe("hidden", "a denúncia tardia continua pendente depois da decisão nova");
        row.DecidedBy.ShouldBe(adminId);
        row.DecidedAtUtc.ShouldNotBeNull();
        (await ModerationDatabase.ReportsAsync(api.ConnectionString, ct))
            .ShouldAllBe(report => report.Status == "hidden", "sobrou denúncia pendente do show oculto");

        // Sem pendentes, decidir de novo continua 404.
        using var again = await admin.PostAsync(ModerationRequests.Hide(show.Id), new { note = (string?)null }, ct);
        await again.ShouldBeProblemAsync(HttpStatusCode.NotFound, "moderation.no_pending_reports", ct);
    }

    [Fact]
    [Trait("Spec", "SPEC-0037:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task ShowHeadlinesReturnsTheTimeZoneOfTheShowsCity()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ModerationApiFactory.AdminEmail, ct);
        using var _ = admin;
        var manaus = await admin.CreateCityAsync("Manaus", "AM", "America/Manaus", ct);
        var north = await admin.CreateShowAsync("Noite do Norte", ct, city: manaus);
        var south = await admin.CreateShowAsync("Noite do Sul", ct);

        await using var scope = api.Services.CreateAsyncScope();
        var headlines = await scope.ServiceProvider.GetRequiredService<IShowHeadlines>().GetAsync([north.Id, south.Id], ct);

        headlines.Keys.ShouldBe([north.Id, south.Id], ignoreOrder: true);
        headlines[north.Id].CitySlug.ShouldBe(manaus);
        headlines[north.Id].TimeZone.ShouldBe("America/Manaus", "ShowHeadline sem o fuso da cidade do show");
        headlines[south.Id].TimeZone.ShouldBe("America/Sao_Paulo", "ShowHeadline sem o fuso da cidade do show");
    }
}
