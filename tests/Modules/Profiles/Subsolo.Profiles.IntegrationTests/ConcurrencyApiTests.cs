using System.Globalization;
using System.Net;
using Subsolo.IntegrationTests.Common;
using Subsolo.Profiles.IntegrationTests.Infrastructure;

namespace Subsolo.Profiles.IntegrationTests;

/// <summary>
/// SPEC-0021 (Emenda 3, linha "Concorrência" do Mapa) — com a API e o PostgreSQL reais, nunca 500 e um só vencedor:
/// <list type="bullet">
/// <item>IT-02: N admins aprovando o mesmo pedido pendente ao mesmo tempo → exatamente um 200, o resto 409
/// <c>profiles.request_decided</c>, um só perfil e um só evento na outbox; aprovar a reivindicação de um perfil que
/// sumiu → 404 <c>profiles.not_found</c>; aprovar um pedido novo cujo nome foi tomado no meio → 409
/// <c>profiles.name_taken</c>.</item>
/// <item>IT-03: duas reivindicações pendentes do mesmo perfil sem dono aprovadas ao mesmo tempo → um 200 e um 409
/// <c>profiles.already_owned</c>, um só dono (o vencedor).</item>
/// <item>IT-01: o mesmo usuário mandando pedido e reivindicação (ou dois pedidos) ao mesmo tempo → um 201 e um 409
/// <c>profiles.request_pending</c>, um só pedido pendente; dois usuários pedindo o mesmo tipo e nome ao mesmo tempo e os
/// dois aprovados ao mesmo tempo → sem 500, o perdedor com 409 <c>profiles.name_taken</c>.</item>
/// </list>
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ConcurrencyApiTests(PostgresFixture postgres)
{
    private const string OwnerAssignedType = "ProfileOwnerAssigned";
    private const string Proof = "Sou o dono; o Instagram da casa confirma.";

    private async Task<ProfilesApiFactory> FactoryAsync(CancellationToken ct) =>
        new(await ProfilesApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

    private static string Name(string prefix, int round) => string.Create(CultureInfo.InvariantCulture, $"{prefix} {round:00}");

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task AdminsApprovingTheSameRequestAtOnceGetOne200AndTheRest409RequestDecided()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var admins = await ConcurrentSends.AdminsAsync(api, ct);
        try
        {
            for (var round = 0; round < ConcurrentSends.Rounds; round++)
            {
                var (fan, fanId) = await api.SignedInAsync(string.Create(CultureInfo.InvariantCulture, $"corrida-aprova-{round}@subsolo.test"), ct);
                using (fan)
                {
                    var name = Name("Banda Disputada", round);
                    var requestId = await fan.RequestProfileAsync(ProfilesRequests.Band(name), ct);

                    await ConcurrentSends.ShouldSucceedOnceAsync(
                        [.. admins.Select(admin => new ConcurrentSends.Send(admin.Client, admin.Token, ProfilesRequests.Approve(requestId), null))],
                        HttpStatusCode.OK,
                        ct,
                        "profiles.request_decided");

                    var profiles = (await ProfilesDatabase.ProfilesAsync(api.ConnectionString, ct)).Where(profile => profile.Name == name).ToList();
                    profiles.ShouldHaveSingleItem($"rodada {round}: a aprovação concorrente criou mais de um perfil").OwnerUserId.ShouldBe(fanId);
                    (await ProfilesDatabase.RequestsAsync(api.ConnectionString, ct)).Single(request => request.Id == requestId)
                        .ProfileId.ShouldBe(profiles[0].Id);
                }
            }

            (await ProfilesDatabase.OutboxContentsAsync(api.ConnectionString, OwnerAssignedType, ct)).Count
                .ShouldBe(ConcurrentSends.Rounds, "um só evento do papel artist por pedido aprovado");
        }
        finally
        {
            admins.ForEach(admin => admin.Client.Dispose());
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task ApprovingAClaimWhoseProfileVanishedIs404NotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        var (fan, _) = await api.SignedInAsync("fa.perfil.sumiu@subsolo.test", ct);
        using var _ = admin;
        using var __ = fan;
        var profileId = await admin.CreateOwnerlessAsync(ProfilesRequests.Ownerless("Porão Que Sumiu"), ct);
        var claimId = await fan.ClaimAsync(profileId, Proof, ct);
        await ProfilesDatabase.DeleteProfileAsync(api.ConnectionString, profileId, ct);

        using var response = await admin.PostAsync(ProfilesRequests.Approve(claimId), null, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "profiles.not_found", ct);
        (await ProfilesDatabase.OutboxContentsAsync(api.ConnectionString, OwnerAssignedType, ct)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task ApprovingANewRequestWhoseNameWasTakenMeanwhileIs409NameTaken()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        var (first, firstId) = await api.SignedInAsync("primeira.podridao@subsolo.test", ct);
        var (second, _) = await api.SignedInAsync("segunda.podridao@subsolo.test", ct);
        using var _ = admin;
        using var __ = first;
        using var ___ = second;
        var firstRequest = await first.RequestProfileAsync(ProfilesRequests.Band("Podridão"), ct);
        var secondRequest = await second.RequestProfileAsync(ProfilesRequests.Band("Podridão"), ct);
        var profileId = await admin.ApproveAsync(firstRequest, ct);

        using var response = await admin.PostAsync(ProfilesRequests.Approve(secondRequest), null, ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "profiles.name_taken", ct);
        (await ProfilesDatabase.ProfilesAsync(api.ConnectionString, ct)).ShouldHaveSingleItem().Id.ShouldBe(profileId);
        (await ProfilesDatabase.ProfilesAsync(api.ConnectionString, ct)).Single().OwnerUserId.ShouldBe(firstId);
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task TwoClaimsOfTheSameProfileApprovedAtOnceGiveOneOwner()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var admins = await ConcurrentSends.AdminsAsync(api, ct, count: 2);
        try
        {
            for (var round = 0; round < ConcurrentSends.Rounds; round++)
            {
                var profileId = await admins[0].Client.CreateOwnerlessAsync(ProfilesRequests.Ownerless(Name("Porão Disputado", round)), ct);
                var (fanA, fanAId) = await api.SignedInAsync(string.Create(CultureInfo.InvariantCulture, $"corrida-claim-a-{round}@subsolo.test"), ct);
                var (fanB, fanBId) = await api.SignedInAsync(string.Create(CultureInfo.InvariantCulture, $"corrida-claim-b-{round}@subsolo.test"), ct);
                using (fanA)
                using (fanB)
                {
                    var claimA = await fanA.ClaimAsync(profileId, Proof, ct);
                    var claimB = await fanB.ClaimAsync(profileId, Proof, ct);

                    var winner = await ConcurrentSends.ShouldSucceedOnceAsync(
                        [
                            new ConcurrentSends.Send(admins[0].Client, admins[0].Token, ProfilesRequests.Approve(claimA), null),
                            new ConcurrentSends.Send(admins[1].Client, admins[1].Token, ProfilesRequests.Approve(claimB), null),
                        ],
                        HttpStatusCode.OK,
                        ct,
                        "profiles.already_owned");

                    var owner = (await ProfilesDatabase.ProfilesAsync(api.ConnectionString, ct)).Single(profile => profile.Id == profileId).OwnerUserId;
                    owner.ShouldBe(winner == 0 ? fanAId : fanBId, $"rodada {round}: o dono gravado não é o da aprovação que venceu");
                    (await ProfilesDatabase.RequestsAsync(api.ConnectionString, ct))
                        .Count(request => request.ProfileId == profileId && request.Status == "approved")
                        .ShouldBe(1, $"rodada {round}: as duas reivindicações ficaram aprovadas");
                }
            }
        }
        finally
        {
            admins.ForEach(admin => admin.Client.Dispose());
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task TheSameUserSendingARequestAndAClaimAtOnceGetsOne201AndOne409RequestPending()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        using var _ = admin;
        for (var round = 0; round < ConcurrentSends.Rounds; round++)
        {
            var profileId = await admin.CreateOwnerlessAsync(ProfilesRequests.Ownerless(Name("Porão Pedido Duplo", round)), ct);
            var email = string.Create(CultureInfo.InvariantCulture, $"corrida-pendente-{round}@subsolo.test");
            var (fan, fanId) = await api.SignedInAsync(email, ct);
            using var otherTab = api.CreateProfilesClient();
            await otherTab.SignInAsync(email, ct);
            using (fan)
            {
                ConcurrentSends.Send[] sends = round % 2 == 0
                    ?
                    [
                        new(fan, await fan.CsrfTokenAsync(ct), ProfilesRequests.Requests, ProfilesRequests.Band(Name("Banda Pedido Duplo", round))),
                        new(otherTab, await otherTab.CsrfTokenAsync(ct), ProfilesRequests.Claims(profileId), new { proof = Proof }),
                    ]
                    :
                    [
                        new(fan, await fan.CsrfTokenAsync(ct), ProfilesRequests.Requests, ProfilesRequests.Band(Name("Banda Pedido Um", round))),
                        new(otherTab, await otherTab.CsrfTokenAsync(ct), ProfilesRequests.Requests, ProfilesRequests.Band(Name("Banda Pedido Dois", round))),
                    ];

                await ConcurrentSends.ShouldSucceedOnceAsync(sends, HttpStatusCode.Created, ct, "profiles.request_pending");

                (await ProfilesDatabase.RequestsAsync(api.ConnectionString, ct))
                    .Count(request => request.RequesterUserId == fanId && request.Status == "pending")
                    .ShouldBe(1, $"rodada {round}: mais de um pedido pendente do mesmo usuário");
            }
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task TwoUsersRequestingTheSameNameAtOnceAndBothApprovedAtOnceNeverGet500()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var admins = await ConcurrentSends.AdminsAsync(api, ct, count: 2);
        try
        {
            for (var round = 0; round < ConcurrentSends.Rounds; round++)
            {
                var name = Name("Banda Homônima", round);
                var (fanA, _) = await api.SignedInAsync(string.Create(CultureInfo.InvariantCulture, $"corrida-nome-a-{round}@subsolo.test"), ct);
                var (fanB, _) = await api.SignedInAsync(string.Create(CultureInfo.InvariantCulture, $"corrida-nome-b-{round}@subsolo.test"), ct);
                using (fanA)
                using (fanB)
                {
                    var requests = await SendTogetherAsync(
                        [
                            new ConcurrentSends.Send(fanA, await fanA.CsrfTokenAsync(ct), ProfilesRequests.Requests, ProfilesRequests.Band(name)),
                            new ConcurrentSends.Send(fanB, await fanB.CsrfTokenAsync(ct), ProfilesRequests.Requests, ProfilesRequests.Band(name)),
                        ],
                        ct);
                    var pending = (await ProfilesDatabase.RequestsAsync(api.ConnectionString, ct))
                        .Where(request => request.Status == "pending" && requests.Contains(request.Id))
                        .Select(request => request.Id)
                        .ToList();
                    pending.Count.ShouldBe(2, $"rodada {round}: os dois pedidos com o mesmo nome (de pessoas diferentes) ficam em análise");

                    await ConcurrentSends.ShouldSucceedOnceAsync(
                        [
                            new ConcurrentSends.Send(admins[0].Client, admins[0].Token, ProfilesRequests.Approve(pending[0]), null),
                            new ConcurrentSends.Send(admins[1].Client, admins[1].Token, ProfilesRequests.Approve(pending[1]), null),
                        ],
                        HttpStatusCode.OK,
                        ct,
                        "profiles.name_taken");

                    (await ProfilesDatabase.ProfilesAsync(api.ConnectionString, ct)).Count(profile => profile.Name == name)
                        .ShouldBe(1, $"rodada {round}: dois perfis com o mesmo tipo e nome");
                }
            }
        }
        finally
        {
            admins.ForEach(admin => admin.Client.Dispose());
        }
    }

    /// <summary>Manda os pedidos ao mesmo tempo, exige 201 (nunca 500) em todos e devolve os <c>requestId</c>.</summary>
    private static async Task<List<Guid>> SendTogetherAsync(IReadOnlyList<ConcurrentSends.Send> sends, CancellationToken ct)
    {
        using var start = new SemaphoreSlim(0, sends.Count);
        var tasks = sends.Select(async send =>
        {
            await start.WaitAsync(ct);
            return await send.Client.SendAsync(HttpMethod.Post, send.Route, ct, send.Body, send.Token);
        }).ToList();
        start.Release(sends.Count);
        var ids = new List<Guid>();
        foreach (var response in await Task.WhenAll(tasks))
        {
            using (response)
            {
                var created = await response.ShouldBeJsonAsync(HttpStatusCode.Created, ct);
                ids.Add(created.GetProperty("requestId").GetGuid());
            }
        }

        return ids;
    }
}
