using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Moderation.IntegrationTests.Infrastructure;

namespace Subsolo.Moderation.IntegrationTests;

/// <summary>
/// SPEC-0033 IT-01 — com a API, o PostgreSQL e um show publicado (cadastrado pelo admin): o fã denuncia (201
/// <c>{ id }</c>, uma linha <c>pending</c> em <c>moderation.reports</c> com o autor, o motivo e o detalhe), denuncia de novo
/// o mesmo show (409 <c>moderation.already_reported</c>, nada gravado), manda motivo desconhecido, <c>other</c> sem detalhe
/// e detalhe com 501 caracteres (400 <c>moderation.invalid_report</c> com o campo) e denuncia um id inexistente (404
/// <c>events.show_not_found</c>). Outro fã denuncia o mesmo show normalmente (a unicidade é por fã e show).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ReportShowApiTests(PostgresFixture postgres)
{
    private async Task<ModerationApiFactory> FactoryAsync(CancellationToken ct) =>
        new(await ModerationApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

    [Fact]
    [Trait("Spec", "SPEC-0033:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task TheFanReportsAPublishedShowAnd201StoresAPendingReport()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ModerationApiFactory.AdminEmail, ct);
        var (fan, fanId) = await api.SignedInAsync("fa.denuncia@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        var show = await admin.CreateShowAsync("Noite da Peste", ct);

        var reportId = await fan.ReportAsync(show.Id, "cancelled_unannounced", ct, "A casa avisou que mudou de data.");

        var row = (await ModerationDatabase.ReportsAsync(api.ConnectionString, ct)).ShouldHaveSingleItem("a denúncia não foi gravada");
        row.Id.ShouldBe(reportId);
        row.ShowId.ShouldBe(show.Id);
        row.ReporterUserId.ShouldBe(fanId);
        row.Reason.ShouldBe("cancelled_unannounced");
        row.Details.ShouldBe("A casa avisou que mudou de data.");
        row.Status.ShouldBe("pending");
        row.DecidedAtUtc.ShouldBeNull();
        row.DecidedBy.ShouldBeNull();
    }

    [Fact]
    [Trait("Spec", "SPEC-0033:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task ReportingTheSameShowAgainIs409AndAnotherFanCanReportIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ModerationApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.repete@subsolo.test", ct);
        var (other, _) = await api.SignedInAsync("outro.fa.denuncia@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        using var ___ = other;
        var show = await admin.CreateShowAsync("Noite Repetida", ct);
        await fan.ReportAsync(show.Id, "fake", ct);

        using (var again = await fan.PostAsync(ModerationRequests.Reports(show.Id), ModerationRequests.ReportBody("wrong_info"), ct))
        {
            await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "moderation.already_reported", ct);
        }

        await other.ReportAsync(show.Id, "fake", ct);
        (await ModerationDatabase.ReportsAsync(api.ConnectionString, ct)).Count.ShouldBe(2, "a denúncia repetida foi gravada (ou a do outro fã não)");
    }

    [Theory]
    [Trait("Spec", "SPEC-0033:IT-01")]
    [Trait("Kind", "Integration")]
    [InlineData("spam", "Qualquer coisa.", "reason")]
    [InlineData(null, null, "reason")]
    [InlineData("other", null, "details")]
    [InlineData("other", "   ", "details")]
    [InlineData("wrong_info", "501", "details")]
    public async Task AnInvalidReportIs400WithTheField(string? reason, string? details, string field)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ModerationApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.invalida@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        var show = await admin.CreateShowAsync("Noite Inválida", ct);
        var body = ModerationRequests.ReportBody(reason, details == "501" ? new string('a', 501) : details);

        using var response = await fan.PostAsync(ModerationRequests.Reports(show.Id), body, ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "moderation.invalid_report", ct);
        problem.ShouldHaveFieldError(field);
        (await ModerationDatabase.ReportsAsync(api.ConnectionString, ct)).ShouldBeEmpty("a denúncia inválida foi gravada");
    }

    /// <summary>
    /// SPEC-0039 (H39-01) — o corpo sem a propriedade <c>reason</c> (não só com <c>reason: null</c>) chega ao validador e dá
    /// 400 <c>moderation.invalid_report</c> com o campo, não um erro de leitura do corpo.
    /// </summary>
    [Fact]
    [Trait("Spec", "SPEC-0039:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task AReportWithoutTheReasonPropertyIs400WithTheField()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ModerationApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.sem.motivo@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        var show = await admin.CreateShowAsync("Noite Sem Motivo", ct);

        using var response = await fan.PostAsync(ModerationRequests.Reports(show.Id), new { details = "x" }, ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "moderation.invalid_report", ct);
        problem.ShouldHaveFieldError("reason");
        (await ModerationDatabase.ReportsAsync(api.ConnectionString, ct)).ShouldBeEmpty("a denúncia sem motivo foi gravada");
    }

    [Fact]
    [Trait("Spec", "SPEC-0033:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task ReportingAnUnknownShowIs404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (fan, _) = await api.SignedInAsync("fa.inexistente@subsolo.test", ct);
        using var _ = fan;

        using var response = await fan.PostAsync(
            ModerationRequests.Reports(Guid.CreateVersion7()), ModerationRequests.ReportBody("fake"), ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "events.show_not_found", ct);
        (await ModerationDatabase.ReportsAsync(api.ConnectionString, ct)).ShouldBeEmpty();
    }
}
