using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Moderation.IntegrationTests.Infrastructure;

namespace Subsolo.Moderation.IntegrationTests;

/// <summary>
/// SPEC-0039 IT-01 (H37-02) — a denúncia e a decisão de ocultar se serializam por show (<c>pg_advisory_xact_lock</c>
/// pela chave <see cref="ModerationDatabase.ShowLockKeySql"/>): com uma decisão em curso (bloqueio tomado e denúncias já
/// <c>hidden</c>, sem commit), a denúncia de outro fã espera o commit e então vê o show oculto — 404
/// <c>events.show_not_found</c>, sem linha nova <c>pending</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ReportDecisionRaceTests(PostgresFixture postgres)
{
    /// <summary>Quanto a denúncia precisa continuar esperando enquanto a decisão segura o bloqueio.</summary>
    private static readonly TimeSpan HeldFor = TimeSpan.FromSeconds(2);

    /// <summary>Prazo para a denúncia responder depois do commit da decisão.</summary>
    private static readonly TimeSpan AfterCommit = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Spec", "SPEC-0039:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task AReportDuringAnInFlightHideWaitsForTheCommitAndIs404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new ModerationApiFactory(
            await ModerationApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, adminId) = await api.SignedInAsync(ModerationApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.antes.da.decisao@subsolo.test", ct);
        var (other, otherId) = await api.SignedInAsync("fa.durante.a.decisao@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        using var ___ = other;
        var show = await admin.CreateShowAsync("Noite da Decisão", ct);
        await fan.ReportAsync(show.Id, "fake", ct);
        // O X-CSRF já em mãos: o POST vai direto ao handler da denúncia.
        var token = await other.CsrfTokenAsync(ct);

        HttpResponseMessage? response = null;
        try
        {
            Task<HttpResponseMessage> report;
            await using (var decision = await ModerationDatabase.BeginHideInFlightAsync(api.ConnectionString, show.Id, adminId, ct))
            {
                decision.Hidden.ShouldBe(1, "a decisão em curso não marcou a denúncia pendente");

                report = Task.Run(
                    () => other.SendAsync(
                        HttpMethod.Post, ModerationRequests.Reports(show.Id), ct, ModerationRequests.ReportBody("wrong_info"), token),
                    ct);

                var first = await Task.WhenAny(report, Task.Delay(HeldFor, ct));
                if (first == report)
                {
                    using var early = await report;
                    Assert.Fail(
                        $"a denúncia respondeu {(int)early.StatusCode} sem esperar a decisão em curso sobre o show " +
                        "(sem pg_advisory_xact_lock pela chave do show)");
                }

                await decision.CommitAsync(ct);
            }

            response = await report.WaitAsync(AfterCommit, ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "events.show_not_found", ct);
        }
        finally
        {
            response?.Dispose();
        }

        var rows = await ModerationDatabase.ReportsAsync(api.ConnectionString, ct);
        rows.ShouldNotContain(row => row.ReporterUserId == otherId, "a denúncia feita durante a decisão foi gravada");
        rows.ShouldAllBe(row => row.Status == "hidden", "sobrou denúncia pendente do show oculto");
    }
}
