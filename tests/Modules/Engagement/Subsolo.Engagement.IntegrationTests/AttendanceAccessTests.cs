using System.Net;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0029 IT-02 — quatro <c>PUT</c> iguais do mesmo fã ao mesmo tempo (em várias rodadas): todos 204, nunca 409 nem
/// 500, e um "vou" só (a chave primária <c>(user_id, show_id)</c> decide a corrida). Anônimo em <c>PUT</c>/<c>DELETE</c>
/// e em <c>GET /me/attendances</c> → 401 <c>auth.required</c>; fã sem <c>X-CSRF</c> (ou com token inválido) → 400
/// <c>security.csrf_invalid</c>; nada muda no banco nem na contagem. A contagem é pública (anônimo → 200).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AttendanceAccessTests(PostgresFixture postgres)
{
    private const int Parallel = 4;

    private const int Rounds = 5;

    private async Task<EngagementApiFactory> FactoryAsync(CancellationToken ct) =>
        new(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

    [Fact]
    [Trait("Spec", "SPEC-0029:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task FourParallelPutsAreAll204AndLeaveOneAttendance()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, fanId) = await api.SignedInAsync("fa.corrida.vou@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        var (show, _) = await admin.CreateShowAsync("Noite da Peste II", ct);
        var route = AttendanceRequests.Attend(show);

        for (var round = 0; round < Rounds; round++)
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
                    $"rodada {round}, PUTs simultâneos: {string.Join(", ", statuses)}");
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }
            }

            (await AttendanceDatabase.AttendancesOfAsync(api.ConnectionString, fanId, ct))
                .ShouldHaveSingleItem($"rodada {round}: a corrida deixou mais de um vou (ou nenhum)");
            await fan.DeleteAsync(route, ct).ShouldSucceedAsync(ct);
        }
    }

    [Theory]
    [Trait("Spec", "SPEC-0029:IT-02")]
    [Trait("Kind", "Integration")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task AnonymousMutationIs401AndChangesNothing(string method)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.anonimo.vou@subsolo.test", ct);
        using var anonymous = api.CreateEngagementClient();
        using var _ = admin;
        using var __ = fan;
        var (show, _) = await admin.CreateShowAsync("Noite da Peste II", ct);
        // Um "vou" existente: o DELETE anônimo não pode removê-lo.
        await fan.PutAsync(AttendanceRequests.Attend(show), ct).ShouldSucceedAsync(ct);
        var before = await AttendanceDatabase.AttendancesAsync(api.ConnectionString, ct);

        using var response = await anonymous.MutateAsync(new HttpMethod(method), AttendanceRequests.Attend(show), null, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
        (await AttendanceDatabase.AttendancesAsync(api.ConnectionString, ct)).ShouldBe(before);
        (await anonymous.CountOfAsync(show, ct)).ShouldBe(1);
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task AnonymousListIs401AndTheCountIsPublic()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        using var anonymous = api.CreateEngagementClient();

        using (var list = await anonymous.GetAsync(AttendanceRequests.Upcoming, ct))
        {
            await list.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
        }

        using var counts = await anonymous.GetAsync(AttendanceRequests.Counts([Guid.CreateVersion7()]), ct);
        await counts.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
    }

    [Theory]
    [Trait("Spec", "SPEC-0029:IT-02")]
    [Trait("Kind", "Integration")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task MutationWithoutCsrfIs400AndChangesNothing(string method)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.sem.token.vou@subsolo.test", ct);
        using var anonymous = api.CreateEngagementClient();
        using var _ = admin;
        using var __ = fan;
        var (show, _) = await admin.CreateShowAsync("Noite da Peste II", ct);
        if (method == "DELETE")
        {
            await fan.PutAsync(AttendanceRequests.Attend(show), ct).ShouldSucceedAsync(ct);
        }

        var before = await AttendanceDatabase.AttendancesAsync(api.ConnectionString, ct);
        var countBefore = await anonymous.CountOfAsync(show, ct);

        using (var withoutToken = await fan.SendAsync(new HttpMethod(method), AttendanceRequests.Attend(show), ct))
        {
            await withoutToken.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
        }

        using (var invalidToken = await fan.SendAsync(new HttpMethod(method), AttendanceRequests.Attend(show), ct, csrfToken: "token-invalido"))
        {
            await invalidToken.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
        }

        (await AttendanceDatabase.AttendancesAsync(api.ConnectionString, ct)).ShouldBe(before);
        (await anonymous.CountOfAsync(show, ct)).ShouldBe(countBefore);
    }
}
