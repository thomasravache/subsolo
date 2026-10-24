using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.IntegrationTests.Common;
using Subsolo.Moderation.IntegrationTests.Infrastructure;
using Subsolo.Modules.Events.IntegrationEvents;

namespace Subsolo.Moderation.IntegrationTests;

/// <summary>
/// SPEC-0033 IT-03 (Emenda 2, contrato v3) — o que o contrato v3 acrescenta à decisão:
/// <list type="bullet">
/// <item><c>IShowHeadlines.GetAsync</c> devolve, só dos ids encontrados, título, cidade, slug, início e casa do show;
/// depois de ocultar, devolve o visível e omite o oculto;</item>
/// <item>o <c>GET /api/v1/shows/{id}</c> do dono (formulário de edição) devolve o show oculto com <c>hidden: true</c> (e o
/// visível com <c>hidden: false</c>);</item>
/// <item>o <c>/hide</c> com nota a grava em <c>moderation.reports.decision_note</c> (todas as denúncias do show), e a
/// nota não sai na fila nem no export de quem denunciou.</item>
/// </list>
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class HiddenShowContractTests(PostgresFixture postgres)
{
    private const string NoteSentinel = "nota-interna-sigilosa-0033";

    /// <summary>Prazo para a outbox do Moderation e a inbox do Events entregarem o <c>ShowHiddenByModeration</c>.</summary>
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Spec", "SPEC-0033:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task ShowHeadlinesReturnsTheFieldsOfTheFoundShowsOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new ModerationApiFactory(await ModerationApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(ModerationApiFactory.AdminEmail, ct);
        using var _ = admin;
        var show = await admin.CreateShowAsync("Noite das Manchetes", ct);

        await using var scope = api.Services.CreateAsyncScope();
        var headlines = await scope.ServiceProvider.GetRequiredService<IShowHeadlines>()
            .GetAsync([show.Id, Guid.CreateVersion7()], ct);

        var headline = headlines.ShouldHaveSingleItem("IShowHeadlines devolveu id inexistente (ou não devolveu o show)");
        headline.Key.ShouldBe(show.Id);
        headline.Value.ShowId.ShouldBe(show.Id);
        headline.Value.Title.ShouldBe(show.Title);
        headline.Value.CitySlug.ShouldBe(ModerationApiFactory.City);
        headline.Value.Slug.ShouldBe(show.Slug);
        headline.Value.VenueName.ShouldBe("Porão do Rock");
        headline.Value.StartsAtUtc.ShouldBe(LocalToUtc(show.StartsLocal));
    }

    [Fact]
    [Trait("Spec", "SPEC-0033:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task HidingWithANoteKeepsItPrivateAndTheOwnerStillSeesTheHiddenShow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new ModerationApiFactory(await ModerationApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(ModerationApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.nota.interna@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        var visible = await admin.CreateShowAsync("Noite Visível", ct, days: 10);
        var hidden = await admin.CreateShowAsync("Noite Escondida", ct, days: 12);
        await fan.ReportAsync(visible.Id, "wrong_info", ct, "Horário errado.");
        await fan.ReportAsync(hidden.Id, "fake", ct);

        await admin.HideAsync(hidden.Id, NoteSentinel, ct);

        (await ModerationDatabase.DecisionNotesAsync(api.ConnectionString, hidden.Id, ct))
            .ShouldHaveSingleItem("a nota não foi gravada em moderation.reports.decision_note")
            .ShouldBe(NoteSentinel);
        (await ModerationDatabase.DecisionNotesAsync(api.ConnectionString, visible.Id, ct))
            .ShouldAllBe(note => note == null, "a nota foi gravada em denúncia de outro show");
        var queue = await admin.QueueAsync(ct);
        queue.ById("showId", visible.Id).ShouldNotBeNull("o show ainda pendente sumiu da fila");
        queue.ToString().ShouldNotContain(NoteSentinel, Case.Insensitive, "a nota interna saiu na fila");
        using (var export = await fan.GetAsync(ModerationRequests.Export, ct))
        {
            var body = await export.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
            body.GetProperty("sections").TryGetProperty("reports", out var section).ShouldBeTrue($"export sem reports: {body}");
            section.ById("showId", hidden.Id).ShouldNotBeNull($"o export não traz a denúncia do show oculto: {section}");
            body.ToString().ShouldNotContain(NoteSentinel, Case.Insensitive, "a nota interna saiu no export");
        }

        (await Timing.EventuallyAsync(
                async () => await ModerationDatabase.HiddenAtAsync(api.ConnectionString, hidden.Id, ct) is not null,
                DeliveryTimeout,
                ct))
            .ShouldBeTrue("o Events não marcou o show como oculto (hidden_at_utc)");

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var headlines = await scope.ServiceProvider.GetRequiredService<IShowHeadlines>().GetAsync([visible.Id, hidden.Id], ct);
            headlines.Keys.ShouldBe([visible.Id], "IShowHeadlines devolve o show oculto (ou não devolve o visível)");
        }

        using (var hiddenForEdit = await admin.GetAsync(ModerationRequests.Show(hidden.Id), ct))
        {
            var body = await hiddenForEdit.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
            body.GetProperty("id").GetGuid().ShouldBe(hidden.Id);
            body.TryGetProperty("hidden", out var flag).ShouldBeTrue($"GET /shows/{{id}} sem hidden: {body}");
            flag.ValueKind.ShouldBe(JsonValueKind.True, $"o show oculto não traz hidden: true para o dono: {body}");
        }

        using (var visibleForEdit = await admin.GetAsync(ModerationRequests.Show(visible.Id), ct))
        {
            var body = await visibleForEdit.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
            body.GetProperty("hidden").ValueKind.ShouldBe(JsonValueKind.False, body.ToString());
        }
    }

    /// <summary>O instante UTC de <paramref name="startsLocal"/> (<c>aaaa-mm-ddThh:mm</c> em São Paulo).</summary>
    private static DateTimeOffset LocalToUtc(string startsLocal)
    {
        var local = DateTime.ParseExact(startsLocal, "yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var saoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return new DateTimeOffset(local, saoPaulo.GetUtcOffset(local)).ToUniversalTime();
    }
}
