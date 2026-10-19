using System.Net;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0027 IT-03 — anônimo em <c>PUT</c>/<c>DELETE</c> de <c>/me/follows/*</c> e em <c>GET /me/follows</c> → 401
/// <c>auth.required</c>; fã logado sem <c>X-CSRF</c> (ou com token inválido) no <c>PUT</c>/<c>DELETE</c> → 400
/// <c>security.csrf_invalid</c>; em todos os casos nada muda no banco nem nas contagens.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class FollowAccessTests(PostgresFixture postgres)
{
    private const string FanEmail = "fa.sem.token@subsolo.test";

    public static TheoryData<string, string> Mutations() => new()
    {
        { "PUT", "city" },
        { "DELETE", "city" },
        { "PUT", "profile" },
        { "DELETE", "profile" },
    };

    private static string Route(string target, Guid profileId) =>
        target == "city" ? EngagementRequests.FollowCity(EngagementApiFactory.City) : EngagementRequests.FollowProfile(profileId);

    [Theory]
    [Trait("Spec", "SPEC-0027:IT-03")]
    [Trait("Kind", "Integration")]
    [MemberData(nameof(Mutations))]
    public async Task AnonymousMutationIs401AndChangesNothing(string method, string target)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new EngagementApiFactory(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync(FanEmail, ct);
        using var anonymous = api.CreateEngagementClient();
        using var _ = admin;
        using var __ = fan;
        var bandId = await admin.CreateApprovedProfileAsync("Podridão", ct);
        // Um follow existente: o DELETE anônimo não pode removê-lo.
        await fan.PutAsync(Route(target, bandId), ct).ShouldSucceedAsync(ct);
        var before = await EngagementDatabase.FollowsAsync(api.ConnectionString, ct);

        using var response = await anonymous.MutateAsync(new HttpMethod(method), Route(target, bandId), null, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
        (await EngagementDatabase.FollowsAsync(api.ConnectionString, ct)).ShouldBe(before);
    }

    [Fact]
    [Trait("Spec", "SPEC-0027:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task AnonymousListIs401()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new EngagementApiFactory(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        using var anonymous = api.CreateEngagementClient();

        using var response = await anonymous.GetAsync(EngagementRequests.MyFollows, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
    }

    [Theory]
    [Trait("Spec", "SPEC-0027:IT-03")]
    [Trait("Kind", "Integration")]
    [MemberData(nameof(Mutations))]
    public async Task MutationWithoutCsrfIs400AndChangesNothing(string method, string target)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = new EngagementApiFactory(await EngagementApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));
        var (admin, _) = await api.SignedInAsync(EngagementApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync(FanEmail, ct);
        using var anonymous = api.CreateEngagementClient();
        using var _ = admin;
        using var __ = fan;
        var bandId = await admin.CreateApprovedProfileAsync("Podridão", ct);
        if (method == "DELETE")
        {
            await fan.PutAsync(Route(target, bandId), ct).ShouldSucceedAsync(ct);
        }

        var before = await EngagementDatabase.FollowsAsync(api.ConnectionString, ct);
        var countRoute = target == "city"
            ? EngagementRequests.CityFollowers(EngagementApiFactory.City)
            : EngagementRequests.ProfileFollowers(bandId);
        var countBefore = await anonymous.CountAsync(countRoute, ct);

        using (var withoutToken = await fan.SendAsync(new HttpMethod(method), Route(target, bandId), ct))
        {
            await withoutToken.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
        }

        using (var invalidToken = await fan.SendAsync(new HttpMethod(method), Route(target, bandId), ct, csrfToken: "token-invalido"))
        {
            await invalidToken.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
        }

        (await EngagementDatabase.FollowsAsync(api.ConnectionString, ct)).ShouldBe(before);
        (await anonymous.CountAsync(countRoute, ct)).ShouldBe(countBefore);
    }
}
