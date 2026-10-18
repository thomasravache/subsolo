using System.Net;
using System.Text.Json;
using Subsolo.IntegrationTests.Common;
using Subsolo.Profiles.IntegrationTests.Infrastructure;

namespace Subsolo.Profiles.IntegrationTests;

/// <summary>
/// SPEC-0021 IT-02 — dado um pedido pendente, o admin aprova: 200 <c>{profileId}</c>, o perfil gravado tem o dono e a
/// outbox do Profiles tem <c>ProfileOwnerAssigned</c> (só ids), e o perfil aparece em <c>GET /me/profiles</c> do dono;
/// o admin recusa outro com motivo: <c>GET /me/profiles</c> do autor mostra <c>rejected</c> com o motivo e um novo
/// pedido é aceito. Aprovar de novo → 409 <c>profiles.request_decided</c>; pedido inexistente → 404
/// <c>profiles.request_not_found</c>; recusar sem motivo → 400 <c>profiles.invalid_request</c> no campo
/// <c>reason</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ApproveRejectApiTests(PostgresFixture postgres)
{
    private const string OwnerAssignedType = "ProfileOwnerAssigned";
    private const string Reason = "Não achamos material da banda; mande o Bandcamp.";

    private async Task<ProfilesApiFactory> FactoryAsync(CancellationToken ct) =>
        new(await ProfilesApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task ApprovingGivesTheProfileItsOwnerAndPutsOwnerAssignedInTheOutbox()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (fan, fanId) = await api.SignedInAsync("fa.aprovado@subsolo.test", ct);
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        using var _ = fan;
        using var __ = admin;
        var requestId = await fan.RequestProfileAsync(ProfilesRequests.Band("Podridão"), ct);

        var profileId = await admin.ApproveAsync(requestId, ct);

        var profile = (await ProfilesDatabase.ProfilesAsync(api.ConnectionString, ct)).ShouldHaveSingleItem("a aprovação não criou o perfil");
        profile.Id.ShouldBe(profileId);
        profile.OwnerUserId.ShouldBe(fanId);
        profile.Type.ShouldBe("band");
        profile.Name.ShouldBe("Podridão");
        profile.Slug.ShouldBe("podridao");
        profile.City.ShouldBe(ProfilesApiFactory.City);
        var request = (await ProfilesDatabase.RequestsAsync(api.ConnectionString, ct)).ShouldHaveSingleItem();
        request.Status.ShouldBe("approved");
        request.ProfileId.ShouldBe(profileId);
        request.DecidedAtUtc.ShouldNotBeNull();

        var message = (await ProfilesDatabase.OutboxContentsAsync(api.ConnectionString, OwnerAssignedType, ct))
            .ShouldHaveSingleItem("ProfileOwnerAssigned não foi para a outbox (ou foi mais de uma vez)");
        using (var content = JsonDocument.Parse(message))
        {
            var properties = content.RootElement.EnumerateObject().ToDictionary(
                property => property.Name.ToUpperInvariant(), property => property.Value.Clone(), StringComparer.Ordinal);
            properties.Keys.ShouldBeSubsetOf(["PROFILEID", "USERID", "ID", "OCCURREDONUTC"], $"ProfileOwnerAssigned com mais que ids: {message}");
            properties["PROFILEID"].GetGuid().ShouldBe(profileId);
            properties["USERID"].GetGuid().ShouldBe(fanId);
        }

        var mine = await fan.MyProfilesAsync(ct);
        var owned = mine.GetProperty("profiles").ById("id", profileId).ShouldNotBeNull($"o perfil aprovado não aparece em /me/profiles: {mine}");
        owned.GetProperty("type").GetString().ShouldBe("band");
        owned.GetProperty("name").GetString().ShouldBe("Podridão");
        owned.GetProperty("slug").GetString().ShouldBe("podridao");
        owned.GetProperty("city").GetString().ShouldBe(ProfilesApiFactory.City);
        mine.GetProperty("requests").ById("requestId", requestId).ShouldNotBeNull()
            .GetProperty("status").GetString().ShouldBe("approved");

        (await admin.QueueAsync(ct)).ById("requestId", requestId).ShouldBeNull("o pedido aprovado sai da fila");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task RejectingShowsTheReasonInMyProfilesAndANewRequestIsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (fan, _) = await api.SignedInAsync("fa.recusado@subsolo.test", ct);
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        using var _ = fan;
        using var __ = admin;
        var requestId = await fan.RequestProfileAsync(ProfilesRequests.Band("Podridão"), ct);

        await admin.RejectAsync(requestId, Reason, ct);

        var mine = await fan.MyProfilesAsync(ct);
        mine.GetProperty("profiles").GetArrayLength().ShouldBe(0, "recusar não cria perfil");
        var request = mine.GetProperty("requests").ById("requestId", requestId).ShouldNotBeNull(mine.ToString());
        request.GetProperty("status").GetString().ShouldBe("rejected");
        request.GetProperty("reason").GetString().ShouldBe(Reason);
        var row = (await ProfilesDatabase.RequestsAsync(api.ConnectionString, ct)).ShouldHaveSingleItem();
        row.Status.ShouldBe("rejected");
        row.Reason.ShouldBe(Reason);
        row.DecidedAtUtc.ShouldNotBeNull();
        (await ProfilesDatabase.OutboxContentsAsync(api.ConnectionString, OwnerAssignedType, ct)).ShouldBeEmpty();

        var again = await fan.RequestProfileAsync(ProfilesRequests.Band("Podridão"), ct);
        again.ShouldNotBe(requestId, "depois da recusa, o fã pode pedir de novo");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task DecidedUnknownAndReasonlessDecisionsAreRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (fan, _) = await api.SignedInAsync("fa.decidido@subsolo.test", ct);
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        using var _ = fan;
        using var __ = admin;
        var requestId = await fan.RequestProfileAsync(ProfilesRequests.Band("Podridão"), ct);

        using (var reasonless = await admin.PostAsync(ProfilesRequests.Reject(requestId), new { reason = "" }, ct))
        {
            var problem = await reasonless.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "profiles.invalid_request", ct);
            problem.ShouldHaveFieldError("reason");
        }

        await admin.ApproveAsync(requestId, ct);

        using (var twice = await admin.PostAsync(ProfilesRequests.Approve(requestId), null, ct))
        {
            await twice.ShouldBeProblemAsync(HttpStatusCode.Conflict, "profiles.request_decided", ct);
        }

        using (var rejectDecided = await admin.PostAsync(ProfilesRequests.Reject(requestId), new { reason = Reason }, ct))
        {
            await rejectDecided.ShouldBeProblemAsync(HttpStatusCode.Conflict, "profiles.request_decided", ct);
        }

        using (var unknown = await admin.PostAsync(ProfilesRequests.Approve(Guid.CreateVersion7()), null, ct))
        {
            await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "profiles.request_not_found", ct);
        }

        using (var unknownReject = await admin.PostAsync(ProfilesRequests.Reject(Guid.CreateVersion7()), new { reason = Reason }, ct))
        {
            await unknownReject.ShouldBeProblemAsync(HttpStatusCode.NotFound, "profiles.request_not_found", ct);
        }

        (await ProfilesDatabase.ProfilesAsync(api.ConnectionString, ct)).Count.ShouldBe(1, "só a primeira aprovação criou perfil");
        (await ProfilesDatabase.OutboxContentsAsync(api.ConnectionString, OwnerAssignedType, ct)).Count.ShouldBe(1);
    }
}
