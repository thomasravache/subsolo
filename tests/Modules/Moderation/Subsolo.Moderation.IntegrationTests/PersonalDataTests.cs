using System.Net;
using System.Text.Json;
using Subsolo.IntegrationTests.Common;
using Subsolo.Moderation.IntegrationTests.Infrastructure;

namespace Subsolo.Moderation.IntegrationTests;

/// <summary>
/// SPEC-0033 IT-05 — dado um fã com duas denúncias (e outro fã com uma): <c>GET /me/export</c> traz a seção
/// <c>reports</c> só com as dele, cada uma com <c>{ showId, reason, details, status, createdAtUtc }</c> (Emenda 1);
/// quando o <c>UserDeleted</c> dele é processado (exclusão da conta pela API, pela outbox do Users e a inbox do
/// Moderation), as denúncias dele continuam com <c>reporter_user_id</c> nulo (o histórico de moderação fica, e a pendente
/// continua na fila); as do outro fã não mudam.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PersonalDataTests(PostgresFixture postgres)
{
    private const string FanEmail = "fa.exporta.denuncia@subsolo.test";

    /// <summary>Prazo para a outbox do Users e a inbox do Moderation entregarem o <c>UserDeleted</c>.</summary>
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);

    private static readonly string[] ExportKeys = ["showId", "reason", "details", "status", "createdAtUtc"];

    [Fact]
    [Trait("Spec", "SPEC-0033:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task TheExportHasTheReportsSectionOnlyWithTheFansReports()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new ModerationApiFactory(await ModerationApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(ModerationApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync(FanEmail, ct);
        var (other, _) = await api.SignedInAsync("outro.fa.exporta.denuncia@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        using var ___ = other;
        var first = await admin.CreateShowAsync("Noite Exportada", ct, days: 10);
        var second = await admin.CreateShowAsync("Noite Exportada II", ct, days: 11);
        await fan.ReportAsync(first.Id, "fake", ct);
        await fan.ReportAsync(second.Id, "other", ct, "Detalhe só meu.");
        await other.ReportAsync(first.Id, "wrong_info", ct, "Detalhe de outra pessoa.");
        await admin.KeepAsync(second.Id, ct);

        using var response = await fan.GetAsync(ModerationRequests.Export, ct);

        var export = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        export.GetProperty("sections").TryGetProperty("reports", out var section)
            .ShouldBeTrue($"o export não tem a seção reports: {export.GetProperty("sections")}");
        section.ValueKind.ShouldBe(JsonValueKind.Array, $"a seção reports não é a lista das denúncias: {section}");
        section.GetArrayLength().ShouldBe(2, section.ToString());
        foreach (var item in section.EnumerateArray())
        {
            item.EnumerateObject().Select(property => property.Name).ShouldBe(ExportKeys, ignoreOrder: true, item.ToString());
        }

        var firstItem = section.ById("showId", first.Id).ShouldNotBeNull(section.ToString());
        firstItem.GetProperty("reason").GetString().ShouldBe("fake");
        firstItem.GetProperty("status").GetString().ShouldBe("pending");
        var secondItem = section.ById("showId", second.Id).ShouldNotBeNull(section.ToString());
        secondItem.GetProperty("reason").GetString().ShouldBe("other");
        secondItem.GetProperty("details").GetString().ShouldBe("Detalhe só meu.");
        secondItem.GetProperty("status").GetString().ShouldBe("kept");
        section.ToString().ShouldNotContain("Detalhe de outra pessoa.", Case.Insensitive, "a seção traz denúncia de outro fã");
    }

    [Fact]
    [Trait("Spec", "SPEC-0033:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task WhenTheFanDeletesTheAccountTheReportsStayWithoutAuthor()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new ModerationApiFactory(await ModerationApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(ModerationApiFactory.AdminEmail, ct);
        var (fan, fanId) = await api.SignedInAsync(FanEmail, ct);
        var (other, otherId) = await api.SignedInAsync("outro.fa.fica.denuncia@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        using var ___ = other;
        var show = await admin.CreateShowAsync("Noite Sem Autor", ct);
        var mine = await fan.ReportAsync(show.Id, "fake", ct);
        var theirs = await other.ReportAsync(show.Id, "wrong_info", ct);
        (await ModerationDatabase.ReportsAsync(api.ConnectionString, ct)).Count.ShouldBe(2);

        using (var delete = await fan.MutateAsync(HttpMethod.Delete, ModerationRequests.Me, new { confirmEmail = FanEmail }, ct))
        {
            delete.StatusCode.ShouldBe(HttpStatusCode.NoContent, ModerationApiClient.Excerpt(await delete.Content.ReadAsStringAsync(ct)));
        }

        (await Timing.EventuallyAsync(
                async () => (await ModerationDatabase.ReportsAsync(api.ConnectionString, ct)).All(row => row.ReporterUserId != fanId),
                DeliveryTimeout,
                ct))
            .ShouldBeTrue("as denúncias do usuário excluído continuam com o autor");

        var rows = await ModerationDatabase.ReportsAsync(api.ConnectionString, ct);
        rows.Count.ShouldBe(2, "a denúncia do usuário excluído foi apagada (o histórico de moderação some)");
        rows.Single(row => row.Id == mine).ReporterUserId.ShouldBeNull();
        rows.Single(row => row.Id == mine).Status.ShouldBe("pending");
        rows.Single(row => row.Id == theirs).ReporterUserId.ShouldBe(otherId, "a denúncia de outro fã perdeu o autor");
        var item = (await admin.QueueAsync(ct)).ById("showId", show.Id).ShouldNotBeNull("o show saiu da fila com a exclusão da conta");
        item.GetProperty("counts").GetProperty("fake").GetInt32().ShouldBe(1, "a denúncia sem autor saiu da contagem");
        item.GetProperty("counts").GetProperty("wrong_info").GetInt32().ShouldBe(1);
    }
}
