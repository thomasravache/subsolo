using System.Net;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0029 IT-04 (ADR-0008: só a contagem é pública) — dado um fã com "vou" marcados: outro usuário (logado ou
/// anônimo) consultando as rotas públicas (contagens, página do show, agenda) não vê o id, o e-mail nem nada de quem vai,
/// e a lista <c>/me/attendances</c> de outro fã não traz os "vou" dele; <c>GET /me/export</c> traz a seção
/// <c>attendances</c> com os shows do fã (e nada de outro fã); quando o <c>UserDeleted</c> dele é processado (exclusão
/// da conta, outbox do Users → inbox do Engagement), os "vou" dele somem e a contagem cai; os de outro fã ficam.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AttendancePrivacyTests(PostgresFixture postgres)
{
    private const string FanEmail = "fa.privado.vou@subsolo.test";

    /// <summary>Prazo para a outbox do Users e a inbox do Engagement entregarem o <c>UserDeleted</c>.</summary>
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);

    private async Task<EngagementApiFactory> FactoryAsync(CancellationToken ct) =>
        new(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

    [Fact]
    [Trait("Spec", "SPEC-0029:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task NoPublicRouteExposesWhoAttends()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, fanId) = await api.SignedInAsync(FanEmail, ct);
        var (other, otherId) = await api.SignedInAsync("curioso.vou@subsolo.test", ct);
        using var anonymous = api.CreateEngagementClient();
        using var _ = admin;
        using var __ = fan;
        using var ___ = other;
        var (show, slug) = await admin.CreateShowAsync("Noite da Peste II", ct);
        await fan.PutAsync(AttendanceRequests.Attend(show), ct).ShouldSucceedAsync(ct);

        foreach (var viewer in new[] { anonymous, other })
        {
            foreach (var route in new[] { AttendanceRequests.Counts([show]), AttendanceRequests.ShowDetail(slug), AttendanceRequests.Agenda })
            {
                using var response = await viewer.GetAsync(route, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                response.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {route}: {EngagementApiClient.Excerpt(body)}");
                body.ShouldNotContain(fanId.ToString(), Case.Insensitive, $"GET {route} expõe o id de quem vai: {body}");
                body.ShouldNotContain(FanEmail, Case.Insensitive, $"GET {route} expõe o e-mail de quem vai: {body}");
                body.ShouldNotContain("attendees", Case.Insensitive, $"GET {route} tem lista de quem vai: {body}");
            }
        }

        // A contagem é só o número.
        (await anonymous.CountsAsync([show], ct)).ShouldBe(new Dictionary<Guid, int> { [show] = 1 });
        // A lista de outro fã não traz o vou do fã.
        (await other.UpcomingAsync(ct)).ShouldBeEmpty("a lista de outro usuário traz o vou do fã");
        otherId.ShouldNotBe(fanId);
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task TheExportHasTheAttendancesSection()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync(FanEmail, ct);
        var (other, _) = await api.SignedInAsync("outro.exporta.vou@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        using var ___ = other;
        var (show, _) = await admin.CreateShowAsync("Noite da Peste II", ct);
        var (otherShow, _) = await admin.CreateShowAsync("Show Só do Outro", ct, days: 11);
        await fan.PutAsync(AttendanceRequests.Attend(show), ct).ShouldSucceedAsync(ct);
        await other.PutAsync(AttendanceRequests.Attend(otherShow), ct).ShouldSucceedAsync(ct);
        // Seções vazias saem do export (SPEC-0019/0027): o fã segue a cidade para a seção follows existir.
        await fan.PutAsync(EngagementRequests.FollowCity(EngagementApiFactory.City), ct).ShouldSucceedAsync(ct);

        using var response = await fan.GetAsync(EngagementRequests.Export, ct);

        var export = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        export.GetProperty("sections").TryGetProperty("attendances", out var section)
            .ShouldBeTrue($"o export não tem a seção attendances: {export.GetProperty("sections")}");
        var text = section.ToString();
        text.ShouldContain(show.ToString(), Case.Insensitive, $"a seção não traz o show marcado: {text}");
        text.ShouldNotContain(otherShow.ToString(), Case.Insensitive, "a seção traz vou de outro fã");
        // A seção de follows continua lá, ao lado da de attendances (SPEC-0027).
        export.GetProperty("sections").TryGetProperty("follows", out var follows).ShouldBeTrue($"a seção follows sumiu: {follows}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0029:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task WhenTheFanDeletesTheAccountTheAttendancesAreDeletedAndTheCountDrops()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, fanId) = await api.SignedInAsync(FanEmail, ct);
        var (other, otherId) = await api.SignedInAsync("outro.fa.fica.vou@subsolo.test", ct);
        using var anonymous = api.CreateEngagementClient();
        using var _ = admin;
        using var __ = fan;
        using var ___ = other;
        var (show, _) = await admin.CreateShowAsync("Noite da Peste II", ct);
        await fan.PutAsync(AttendanceRequests.Attend(show), ct).ShouldSucceedAsync(ct);
        await other.PutAsync(AttendanceRequests.Attend(show), ct).ShouldSucceedAsync(ct);
        (await anonymous.CountOfAsync(show, ct)).ShouldBe(2);

        using (var delete = await fan.MutateAsync(HttpMethod.Delete, EngagementRequests.Me, new { confirmEmail = FanEmail }, ct))
        {
            delete.StatusCode.ShouldBe(HttpStatusCode.NoContent, EngagementApiClient.Excerpt(await delete.Content.ReadAsStringAsync(ct)));
        }

        (await Timing.EventuallyAsync(
                async () => (await AttendanceDatabase.AttendancesOfAsync(api.ConnectionString, fanId, ct)).Count == 0,
                DeliveryTimeout,
                ct))
            .ShouldBeTrue("os vou do usuário excluído não foram apagados");
        (await AttendanceDatabase.AttendancesOfAsync(api.ConnectionString, otherId, ct))
            .ShouldHaveSingleItem("o vou de outro fã foi apagado");
        (await anonymous.CountOfAsync(show, ct)).ShouldBe(1);
    }
}
