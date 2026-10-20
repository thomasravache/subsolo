using System.Net;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0029 IT-01 — a API do "vou" com o PostgreSQL real e shows de verdade (cadastrados pela API do Events): o fã marca
/// (<c>PUT /me/attendances/{showId}</c>, 204), vê o show em <c>GET /me/attendances?period=upcoming</c> (<c>no-store</c>)
/// e a contagem pública (<c>GET /attendances/counts</c>) sobe; outro fã soma; desmarcar (<c>DELETE</c>, 204,
/// idempotente) faz a lista e a contagem voltarem; repetir o <c>PUT</c> não duplica. Show cancelado ou que já passou →
/// 409 <c>engagement.show_not_attendable</c> (o "vou" já marcado num show depois cancelado fica e continua contando);
/// inexistente → 404 <c>engagement.show_not_found</c>. Marcar e desmarcar em ≤ 100 ms p95 (§5).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AttendanceApiTests(PostgresFixture postgres)
{
    private static readonly TimeSpan MutationBudget = TimeSpan.FromMilliseconds(100);

    private async Task<EngagementApiFactory> FactoryAsync(CancellationToken ct, FakeTimeProvider? clock = null) =>
        new(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct), clock);

    [Fact]
    [Trait("Spec", "SPEC-0029:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task TheFanAttendsListsAndUnattendsAndTheCountFollowsEachStep()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, fanId) = await api.SignedInAsync("fa.vou@subsolo.test", ct);
        var (other, _) = await api.SignedInAsync("outro.fa.vou@subsolo.test", ct);
        using var anonymous = api.CreateEngagementClient();
        using var _ = admin;
        using var __ = fan;
        using var ___ = other;
        var (show, _) = await admin.CreateShowAsync("Noite da Peste II", ct);
        var (otherShow, _) = await admin.CreateShowAsync("Sepulcro Fest", ct, days: 12);
        (await anonymous.CountOfAsync(show, ct)).ShouldBe(0);
        (await fan.UpcomingAsync(ct)).ShouldBeEmpty();

        await fan.PutAsync(AttendanceRequests.Attend(show), ct).ShouldSucceedAsync(ct);

        (await fan.UpcomingAsync(ct)).ShouldBe([show]);
        (await anonymous.CountOfAsync(show, ct)).ShouldBe(1);
        (await AttendanceDatabase.AttendancesOfAsync(api.ConnectionString, fanId, ct))
            .ShouldHaveSingleItem().ShowId.ShouldBe(show);

        // Repetir não duplica.
        await fan.PutAsync(AttendanceRequests.Attend(show), ct).ShouldSucceedAsync(ct);
        (await anonymous.CountOfAsync(show, ct)).ShouldBe(1, "repetir o PUT duplicou o vou");

        // Outro fã soma; outro show do fã entra na lista.
        await other.PutAsync(AttendanceRequests.Attend(show), ct).ShouldSucceedAsync(ct);
        await fan.PutAsync(AttendanceRequests.Attend(otherShow), ct).ShouldSucceedAsync(ct);
        var counts = await anonymous.CountsAsync([show, otherShow], ct);
        counts.GetValueOrDefault(show).ShouldBe(2);
        counts.GetValueOrDefault(otherShow).ShouldBe(1);
        (await fan.UpcomingAsync(ct)).ShouldBe([show, otherShow], ignoreOrder: true);
        (await other.UpcomingAsync(ct)).ShouldBe([show], "a lista de um fã traz vou de outro");

        // Desmarcar (e desmarcar de novo: idempotente).
        await fan.DeleteAsync(AttendanceRequests.Attend(show), ct).ShouldSucceedAsync(ct);
        await fan.DeleteAsync(AttendanceRequests.Attend(show), ct).ShouldSucceedAsync(ct);

        (await fan.UpcomingAsync(ct)).ShouldBe([otherShow]);
        (await anonymous.CountOfAsync(show, ct)).ShouldBe(1);
        (await other.UpcomingAsync(ct)).ShouldBe([show], "desmarcar de um fã mexeu no vou de outro");
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task UnattendingAShowNeverAttendedIs204()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.nunca.marcou@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        var (show, _) = await admin.CreateShowAsync("Noite da Peste II", ct);

        await fan.DeleteAsync(AttendanceRequests.Attend(show), ct).ShouldSucceedAsync(ct);
        await fan.DeleteAsync(AttendanceRequests.Attend(Guid.CreateVersion7()), ct).ShouldSucceedAsync(ct);

        (await AttendanceDatabase.AttendancesAsync(api.ConnectionString, ct)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task ACancelledShowIs409AndNothingIsStored()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.cancelado@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        var (show, _) = await admin.CreateShowAsync("Sepulcro Fest", ct);
        await admin.CancelShowAsync(show, ct);

        using var response = await fan.PutAsync(AttendanceRequests.Attend(show), ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "engagement.show_not_attendable", ct);
        (await AttendanceDatabase.AttendancesAsync(api.ConnectionString, ct)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task AnAttendanceMadeBeforeTheCancellationStaysAndKeepsCounting()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, fanId) = await api.SignedInAsync("fa.antes.do.cancelamento@subsolo.test", ct);
        using var anonymous = api.CreateEngagementClient();
        using var _ = admin;
        using var __ = fan;
        var (show, _) = await admin.CreateShowAsync("Sepulcro Fest", ct);
        await fan.PutAsync(AttendanceRequests.Attend(show), ct).ShouldSucceedAsync(ct);

        await admin.CancelShowAsync(show, ct);

        (await anonymous.CountOfAsync(show, ct)).ShouldBe(1, "o vou de show cancelado fica registrado e a contagem continua");
        (await AttendanceDatabase.AttendancesOfAsync(api.ConnectionString, fanId, ct)).ShouldHaveSingleItem();
        using (var again = await fan.PutAsync(AttendanceRequests.Attend(show), ct))
        {
            await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "engagement.show_not_attendable", ct);
        }

        await fan.DeleteAsync(AttendanceRequests.Attend(show), ct).ShouldSucceedAsync(ct);
        (await anonymous.CountOfAsync(show, ct)).ShouldBe(0);
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task APastShowIs409AndLeavesTheUpcomingList()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var api = await FactoryAsync(ct, clock);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.passado@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        var (soon, _) = await admin.CreateShowAsync("Já Foi", ct, days: 1, now: clock.GetUtcNow());
        var (later, _) = await admin.CreateShowAsync("Ainda Vem", ct, days: 30, now: clock.GetUtcNow());
        var (missed, _) = await admin.CreateShowAsync("Perdi", ct, days: 1, now: clock.GetUtcNow());
        await fan.PutAsync(AttendanceRequests.Attend(soon), ct).ShouldSucceedAsync(ct);
        await fan.PutAsync(AttendanceRequests.Attend(later), ct).ShouldSucceedAsync(ct);

        clock.Advance(TimeSpan.FromDays(3));

        using (var response = await fan.PutAsync(AttendanceRequests.Attend(missed), ct))
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "engagement.show_not_attendable", ct);
        }

        (await fan.UpcomingAsync(ct)).ShouldBe([later], "period=upcoming traz show que já passou");
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task AnUnknownShowIs404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (fan, _) = await api.SignedInAsync("fa.show.inexistente@subsolo.test", ct);
        using var _ = fan;

        using var response = await fan.PutAsync(AttendanceRequests.Attend(Guid.CreateVersion7()), ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "engagement.show_not_found", ct);
        (await AttendanceDatabase.AttendancesAsync(api.ConnectionString, ct)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task AttendingAndUnattendingStayWithinTheBudget()
    {
        const int warmup = 3;
        const int samples = 20;
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.rapido@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        var (show, _) = await admin.CreateShowAsync("Noite da Peste II", ct);
        var route = AttendanceRequests.Attend(show);

        var put = new List<TimeSpan>();
        var delete = new List<TimeSpan>();
        for (var i = 0; i < warmup + samples; i++)
        {
            var putToken = await fan.CsrfTokenAsync(ct);
            var (putResponse, putElapsed) = await Timing.TimedAsync(() => fan.SendAsync(HttpMethod.Put, route, ct, csrfToken: putToken));
            using (putResponse)
            {
                await putResponse.ShouldBeNoContentAsync(ct);
            }

            var deleteToken = await fan.CsrfTokenAsync(ct);
            var (deleteResponse, deleteElapsed) = await Timing.TimedAsync(() => fan.SendAsync(HttpMethod.Delete, route, ct, csrfToken: deleteToken));
            using (deleteResponse)
            {
                await deleteResponse.ShouldBeNoContentAsync(ct);
            }

            if (i >= warmup)
            {
                put.Add(putElapsed);
                delete.Add(deleteElapsed);
            }
        }

        Timing.Percentile(put, 95).ShouldBeLessThanOrEqualTo(MutationBudget, $"PUT p95 (amostras: {Join(put)})");
        Timing.Percentile(delete, 95).ShouldBeLessThanOrEqualTo(MutationBudget, $"DELETE p95 (amostras: {Join(delete)})");
    }

    private static string Join(IEnumerable<TimeSpan> values) =>
        string.Join(", ", values.Select(value => $"{value.TotalMilliseconds:F0}"));
}
