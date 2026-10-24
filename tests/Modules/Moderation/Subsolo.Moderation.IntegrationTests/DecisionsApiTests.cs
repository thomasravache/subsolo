using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.IntegrationTests.Common;
using Subsolo.Moderation.IntegrationTests.Infrastructure;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Moderation.IntegrationTests;

/// <summary>
/// SPEC-0033 IT-03 — dois shows denunciados (o "mantido", com duas denúncias, e o "oculto", com uma) e o admin decide:
/// <list type="bullet">
/// <item>a fila agrupa por show (contagens por motivo, detalhes, sem quem denunciou); manter dá 204 e todas as pendentes
/// do show viram <c>kept</c> (com quando e quem decidiu); ocultar dá 204, as pendentes viram <c>hidden</c> e o
/// <c>ShowHiddenByModeration</c> sai pela outbox do Moderation; os dois saem da fila (200 <c>[]</c>);</item>
/// <item>o mantido segue na agenda, na página do show, nos shows do perfil e no <c>IEventsCatalog</c>;</item>
/// <item>o oculto (Emenda 1) ganha <c>events.shows.hidden_at_utc</c>, some da agenda, da página do show (404
/// <c>events.show_not_found</c>) e dos shows do perfil; o <c>IEventsCatalog</c> não o devolve em
/// <c>GetShowSummaryAsync</c>, <c>GetShowStateAsync</c>, <c>GetShowStatesAsync</c> nem
/// <c>ListScheduledShowsStartingAsync</c>; "Meus shows" do dono (o admin que cadastrou) traz <c>hidden: true</c> (e o
/// mantido <c>hidden: false</c>); editar ou mudar a situação dele dá 409 <c>events.show_hidden</c>; denunciá-lo dá 404
/// <c>events.show_not_found</c>;</item>
/// <item>decidir de novo qualquer um dos dois dá 404 <c>moderation.no_pending_reports</c>;</item>
/// <item>o mesmo <c>ShowHiddenByModeration</c> entregue de novo à inbox do Events é processado sem erro e mantém o
/// primeiro <c>hidden_at_utc</c> (consumidor idempotente).</item>
/// </list>
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class DecisionsApiTests(PostgresFixture postgres)
{
    private const string ShowHidden = "ShowHiddenByModeration";

    /// <summary>Prazo para a outbox do Moderation e a inbox do Events (a cada 2 s) entregarem o evento.</summary>
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Spec", "SPEC-0033:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task KeepingAndHidingResolveTheReportsAndTheHiddenShowLeavesEveryPublicRead()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new ModerationApiFactory(await ModerationApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, adminId) = await api.SignedInAsync(ModerationApiFactory.AdminEmail, ct);
        var (fan, fanId) = await api.SignedInAsync("fa.denuncia.dois@subsolo.test", ct);
        var (other, otherId) = await api.SignedInAsync("outro.fa.decisao@subsolo.test", ct);
        var (late, _) = await api.SignedInAsync("fa.atrasado@subsolo.test", ct);
        using var anonymous = api.CreateModerationClient();
        using var _ = admin;
        using var __ = fan;
        using var ___ = other;
        using var ____ = late;
        var kept = await admin.CreateShowAsync("Noite Mantida", ct, days: 10);
        var hidden = await admin.CreateShowAsync("Noite Oculta", ct, days: 12);
        await fan.ReportAsync(kept.Id, "fake", ct);
        await other.ReportAsync(kept.Id, "other", ct, "Essa banda nem existe.");
        await fan.ReportAsync(hidden.Id, "wrong_info", ct, "O line-up está errado.");

        // A fila: agrupada por show, sem quem denunciou.
        var queue = await admin.QueueAsync(ct);
        queue.GetArrayLength().ShouldBe(2, queue.ToString());
        var keptItem = queue.ById("showId", kept.Id).ShouldNotBeNull(queue.ToString());
        keptItem.EnumerateObject().Select(property => property.Name).ShouldBe(ModerationRequests.QueueItemKeys, ignoreOrder: true);
        keptItem.GetProperty("title").GetString().ShouldBe(kept.Title);
        keptItem.GetProperty("slug").GetString().ShouldBe(kept.Slug);
        keptItem.GetProperty("citySlug").GetString().ShouldBe(ModerationApiFactory.City);
        keptItem.GetProperty("venueName").GetString().ShouldBe("Porão do Rock");
        keptItem.GetProperty("counts").GetProperty("fake").GetInt32().ShouldBe(1);
        keptItem.GetProperty("counts").GetProperty("other").GetInt32().ShouldBe(1);
        keptItem.GetProperty("counts").GetProperty("wrong_info").GetInt32().ShouldBe(0);
        keptItem.GetProperty("counts").GetProperty("cancelled_unannounced").GetInt32().ShouldBe(0);
        keptItem.GetProperty("latestDetails").EnumerateArray().Select(detail => detail.GetString()).ShouldBe(["Essa banda nem existe."]);
        var text = queue.ToString();
        foreach (var author in new[] { fanId, otherId })
        {
            text.ShouldNotContain(author.ToString(), Case.Insensitive, "a fila identifica quem denunciou");
        }

        text.ShouldNotContain("@subsolo.test", Case.Insensitive, "a fila traz o e-mail de quem denunciou");

        // Decisões.
        await admin.KeepAsync(kept.Id, ct);
        await admin.HideAsync(hidden.Id, "Show inventado, confirmado com a casa.", ct);

        var reports = await ModerationDatabase.ReportsAsync(api.ConnectionString, ct);
        reports.Where(row => row.ShowId == kept.Id).ShouldAllBe(row => row.Status == "kept", "as denúncias do mantido não viraram kept");
        reports.Where(row => row.ShowId == hidden.Id).ShouldAllBe(row => row.Status == "hidden", "as denúncias do oculto não viraram hidden");
        reports.ShouldAllBe(row => row.DecidedAtUtc != null && row.DecidedBy == adminId, "a decisão não registrou quando e quem");
        (await ModerationDatabase.OutboxContentsAsync(api.ConnectionString, ShowHidden, ct))
            .ShouldHaveSingleItem("o ShowHiddenByModeration não saiu (uma vez) pela outbox do Moderation")
            .ShouldContain(hidden.Id.ToString(), Case.Insensitive);
        (await admin.QueueAsync(ct)).GetArrayLength().ShouldBe(0, "os shows decididos continuam na fila");

        (await Timing.EventuallyAsync(
                async () => await ModerationDatabase.HiddenAtAsync(api.ConnectionString, hidden.Id, ct) is not null,
                DeliveryTimeout,
                ct))
            .ShouldBeTrue("o Events não marcou o show como oculto (hidden_at_utc)");
        (await ModerationDatabase.HiddenAtAsync(api.ConnectionString, kept.Id, ct)).ShouldBeNull("o show mantido ficou oculto");

        // Leituras públicas: o mantido segue, o oculto some.
        var agenda = await anonymous.AgendaShowIdsAsync(ct);
        agenda.ShouldContain(kept.Id, "o show mantido saiu da agenda");
        agenda.ShouldNotContain(hidden.Id, "o show oculto continua na agenda");
        using (var keptPage = await anonymous.GetAsync(ModerationRequests.Detail(kept.Slug), ct))
        {
            await keptPage.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        }

        using (var hiddenPage = await anonymous.GetAsync(ModerationRequests.Detail(hidden.Slug), ct))
        {
            await hiddenPage.ShouldBeProblemAsync(HttpStatusCode.NotFound, "events.show_not_found", ct);
        }

        (await ProfileShowIdsAsync(anonymous, kept.OrganizerProfileId, ct)).ShouldContain(kept.Id);
        (await ProfileShowIdsAsync(anonymous, hidden.OrganizerProfileId, ct)).ShouldNotContain(hidden.Id, "o show oculto continua nos shows do perfil");

        // O contrato do Events para os outros módulos.
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<IEventsCatalog>();
            (await catalog.GetShowSummaryAsync(kept.Id, ct)).ShouldNotBeNull();
            (await catalog.GetShowSummaryAsync(hidden.Id, ct)).ShouldBeNull("GetShowSummaryAsync devolve o show oculto");
            (await catalog.GetShowStateAsync(hidden.Id, ct)).ShouldBeNull("GetShowStateAsync devolve o show oculto");
            (await catalog.GetShowStatesAsync([kept.Id, hidden.Id], ct)).Keys.ShouldBe([kept.Id], "GetShowStatesAsync devolve o show oculto");
            var upcoming = await catalog.ListScheduledShowsStartingAsync(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30), ct);
            upcoming.Select(summary => summary.ShowId).ShouldContain(kept.Id);
            upcoming.Select(summary => summary.ShowId).ShouldNotContain(hidden.Id, "ListScheduledShowsStartingAsync devolve o show oculto");
        }

        // "Meus shows" do dono: o oculto com hidden: true, sem edição.
        using (var mine = await admin.GetAsync(ModerationRequests.MyShowsUpcoming, ct))
        {
            var list = await mine.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
            var hiddenItem = list.ById("id", hidden.Id).ShouldNotBeNull($"o show oculto sumiu de Meus shows do dono: {list}");
            hiddenItem.TryGetProperty("hidden", out var flag).ShouldBeTrue($"item de Meus shows sem hidden: {hiddenItem}");
            flag.ValueKind.ShouldBe(JsonValueKind.True, $"o show oculto não traz hidden: true: {hiddenItem}");
            var keptMine = list.ById("id", kept.Id).ShouldNotBeNull(list.ToString());
            keptMine.GetProperty("hidden").ValueKind.ShouldBe(JsonValueKind.False, keptMine.ToString());
        }

        using (var edit = await admin.MutateAsync(
                   HttpMethod.Put,
                   ModerationRequests.Show(hidden.Id),
                   ModerationRequests.ShowBody(hidden.OrganizerProfileId, "Noite Oculta Editada", hidden.StartsLocal, hidden.FlyerId),
                   ct))
        {
            await edit.ShouldBeProblemAsync(HttpStatusCode.Conflict, "events.show_hidden", ct);
        }

        using (var cancel = await admin.PostAsync(ModerationRequests.ShowStatus(hidden.Id), new { status = "cancelled" }, ct))
        {
            await cancel.ShouldBeProblemAsync(HttpStatusCode.Conflict, "events.show_hidden", ct);
        }

        using (var report = await late.PostAsync(ModerationRequests.Reports(hidden.Id), ModerationRequests.ReportBody("fake"), ct))
        {
            await report.ShouldBeProblemAsync(HttpStatusCode.NotFound, "events.show_not_found", ct);
        }

        // Decidir de novo.
        foreach (var route in new[] { ModerationRequests.Keep(kept.Id), ModerationRequests.Hide(hidden.Id), ModerationRequests.Hide(kept.Id) })
        {
            using var again = await admin.PostAsync(route, route.EndsWith("/hide", StringComparison.Ordinal) ? new { note = (string?)null } : null, ct);
            await again.ShouldBeProblemAsync(HttpStatusCode.NotFound, "moderation.no_pending_reports", ct);
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0033:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task TheSameShowHiddenEventDeliveredTwiceKeepsTheFirstHiddenAt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new ModerationApiFactory(await ModerationApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(ModerationApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.oculta.duas.vezes@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        var show = await admin.CreateShowAsync("Noite Repetida Oculta", ct);
        await fan.ReportAsync(show.Id, "fake", ct);
        await admin.HideAsync(show.Id, null, ct);
        (await Timing.EventuallyAsync(
                async () => await ModerationDatabase.HiddenAtAsync(api.ConnectionString, show.Id, ct) is not null,
                DeliveryTimeout,
                ct))
            .ShouldBeTrue("o Events não marcou o show como oculto (hidden_at_utc)");
        var first = await ModerationDatabase.HiddenAtAsync(api.ConnectionString, show.Id, ct);
        var delivered = (await ModerationDatabase.EventsInboxAsync(api.ConnectionString, ShowHidden, ct))
            .ShouldHaveSingleItem("o ShowHiddenByModeration não está (uma vez) na inbox do Events");

        var copy = await ModerationDatabase.RedeliverToEventsAsync(api.ConnectionString, delivered.Id, ct);
        (await Timing.EventuallyAsync(
                async () => (await ModerationDatabase.EventsInboxAsync(api.ConnectionString, ShowHidden, ct))
                    .Any(row => row.Id == copy && (row.ProcessedOnUtc is not null || row.Error is not null)),
                DeliveryTimeout,
                ct))
            .ShouldBeTrue("o evento entregue de novo não foi processado pela inbox do Events");

        (await ModerationDatabase.EventsInboxAsync(api.ConnectionString, ShowHidden, ct)).Single(row => row.Id == copy)
            .Error.ShouldBeNull("o consumidor falhou com o evento repetido");
        (await ModerationDatabase.HiddenAtAsync(api.ConnectionString, show.Id, ct)).ShouldBe(first, "ocultar de novo mudou o hidden_at_utc");
    }

    /// <summary>Os ids de <c>GET /api/v1/shows?profileId=&amp;period=upcoming</c> (público).</summary>
    private static async Task<IReadOnlyList<Guid>> ProfileShowIdsAsync(ModerationApiClient client, Guid profileId, CancellationToken ct)
    {
        using var response = await client.GetAsync(ModerationRequests.ProfileShows(profileId), ct);
        var body = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        return [.. body.GetProperty("shows").EnumerateArray().Select(show => show.GetProperty("id").GetGuid())];
    }
}
