using System.Net;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0027 IT-05 — dado um fã que segue a cidade e um perfil: <c>GET /me/export</c> traz a seção <c>follows</c> com a
/// cidade e o perfil (e nada de outro fã); quando o <c>UserDeleted</c> dele é processado (exclusão da conta pela API, que
/// publica pela outbox do Users e chega pela inbox do Engagement), os follows dele somem e as contagens públicas caem; os
/// de outro fã ficam.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PersonalDataTests(PostgresFixture postgres)
{
    private const string FanEmail = "fa.exporta.segue@subsolo.test";

    /// <summary>Prazo para a outbox do Users e a inbox do Engagement entregarem o <c>UserDeleted</c>.</summary>
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Spec", "SPEC-0027:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task TheExportHasTheFollowsSectionWithTheCityAndTheProfile()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new EngagementApiFactory(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync(FanEmail, ct);
        var (other, _) = await api.SignedInAsync("outro.fa.exporta@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        using var ___ = other;
        var bandId = await admin.CreateApprovedProfileAsync("Podridão", ct);
        var otherBandId = await admin.CreateApprovedProfileAsync("Banda Só do Outro", ct);
        await fan.PutAsync(EngagementRequests.FollowCity(EngagementApiFactory.City), ct).ShouldSucceedAsync(ct);
        await fan.PutAsync(EngagementRequests.FollowProfile(bandId), ct).ShouldSucceedAsync(ct);
        await other.PutAsync(EngagementRequests.FollowProfile(otherBandId), ct).ShouldSucceedAsync(ct);

        using var response = await fan.GetAsync(EngagementRequests.Export, ct);

        var export = await response.ShouldBeJsonAsync(HttpStatusCode.OK, ct);
        export.GetProperty("sections").TryGetProperty("follows", out var section)
            .ShouldBeTrue($"o export não tem a seção follows: {export.GetProperty("sections")}");
        var text = section.ToString();
        text.ShouldContain(EngagementApiFactory.City, Case.Sensitive, $"a seção não traz a cidade seguida: {text}");
        text.ShouldContain(bandId.ToString(), Case.Insensitive, $"a seção não traz o perfil seguido: {text}");
        text.ShouldContain("Podridão", Case.Sensitive, text);
        text.ShouldNotContain(otherBandId.ToString(), Case.Insensitive, "a seção traz follow de outro fã");
        text.ShouldNotContain("Banda Só do Outro", Case.Insensitive, "a seção traz follow de outro fã");
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task WhenTheFanDeletesTheAccountTheFollowsAreDeletedAndTheCountsDrop()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new EngagementApiFactory(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, fanId) = await api.SignedInAsync(FanEmail, ct);
        var (other, otherId) = await api.SignedInAsync("outro.fa.fica@subsolo.test", ct);
        using var anonymous = api.CreateEngagementClient();
        using var _ = admin;
        using var __ = fan;
        using var ___ = other;
        var bandId = await admin.CreateApprovedProfileAsync("Podridão", ct);
        await fan.PutAsync(EngagementRequests.FollowCity(EngagementApiFactory.City), ct).ShouldSucceedAsync(ct);
        await fan.PutAsync(EngagementRequests.FollowProfile(bandId), ct).ShouldSucceedAsync(ct);
        await other.PutAsync(EngagementRequests.FollowProfile(bandId), ct).ShouldSucceedAsync(ct);
        (await anonymous.CountAsync(EngagementRequests.ProfileFollowers(bandId), ct)).ShouldBe(2);
        (await anonymous.CountAsync(EngagementRequests.CityFollowers(EngagementApiFactory.City), ct)).ShouldBe(1);

        using (var delete = await fan.MutateAsync(HttpMethod.Delete, EngagementRequests.Me, new { confirmEmail = FanEmail }, ct))
        {
            delete.StatusCode.ShouldBe(HttpStatusCode.NoContent, EngagementApiClient.Excerpt(await delete.Content.ReadAsStringAsync(ct)));
        }

        (await Timing.EventuallyAsync(
                async () => (await EngagementDatabase.FollowsOfAsync(api.ConnectionString, fanId, ct)).Count == 0,
                DeliveryTimeout,
                ct))
            .ShouldBeTrue("os follows do usuário excluído não foram apagados");
        (await EngagementDatabase.FollowsOfAsync(api.ConnectionString, otherId, ct))
            .ShouldHaveSingleItem("o follow de outro fã foi apagado");
        (await anonymous.CountAsync(EngagementRequests.ProfileFollowers(bandId), ct)).ShouldBe(1);
        (await anonymous.CountAsync(EngagementRequests.CityFollowers(EngagementApiFactory.City), ct)).ShouldBe(0);
    }
}
