using System.Net;
using System.Text.Json;
using Subsolo.IntegrationTests.Common;
using Subsolo.Profiles.IntegrationTests.Infrastructure;

namespace Subsolo.Profiles.IntegrationTests;

/// <summary>
/// SPEC-0021 IT-01 — com a API e o PostgreSQL reais (cidades e estilos semeados), o fã faz
/// <c>POST /api/v1/profiles/requests</c>: válido → 201 <c>{requestId, status: "pending"}</c>, gravado como pedido
/// <c>new</c> pendente e mostrado em <c>GET /me/profiles</c> (com <c>no-store</c>, sem o contato); inválido → 400
/// <c>profiles.invalid_request</c> com o campo; repetido → 409 <c>profiles.request_pending</c>; nome já usado no tipo →
/// 409 <c>profiles.name_taken</c>. Sem sessão → 401 <c>auth.required</c>; sem <c>X-CSRF</c> → 400
/// <c>security.csrf_invalid</c>, nada gravado.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ProfileRequestApiTests(PostgresFixture postgres)
{
    private const string FanEmail = "fa.pedido@subsolo.test";
    private const string Contact = "zap 41 99999-0001 com o Caio";

    /// <summary>Campo inválido do corpo e a chave esperada em <c>errors</c>.</summary>
    public static readonly TheoryData<string, string> InvalidBodies = new()
    {
        { "nome curto", "name" },
        { "cidade inexistente", "city" },
        { "estilo inexistente", "styles" },
        { "instagram http", "links.instagram" },
        { "instagram de outro domínio", "links.instagram" },
        { "contato curto", "contact" },
        { "tipo desconhecido", "type" },
    };

    private async Task<ProfilesApiFactory> FactoryAsync(CancellationToken ct) =>
        new(await ProfilesApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct));

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task AValidRequestIs201PendingAndShowsInMyProfiles()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (fan, fanId) = await api.SignedInAsync(FanEmail, ct);
        using var _ = fan;

        var requestId = await fan.RequestProfileAsync(ProfilesRequests.Band("Podridão", Contact), ct);

        var row = (await ProfilesDatabase.RequestsAsync(api.ConnectionString, ct)).ShouldHaveSingleItem("o pedido não foi gravado");
        row.Id.ShouldBe(requestId);
        row.Kind.ShouldBe("new");
        row.Status.ShouldBe("pending");
        row.RequesterUserId.ShouldBe(fanId);
        row.ContactOrProof.ShouldBe(Contact);
        (await ProfilesDatabase.ProfilesAsync(api.ConnectionString, ct)).ShouldBeEmpty("o perfil só nasce na aprovação");

        var mine = await fan.MyProfilesAsync(ct);
        mine.GetProperty("profiles").GetArrayLength().ShouldBe(0);
        var request = mine.GetProperty("requests").ById("requestId", requestId).ShouldNotBeNull($"o pedido não aparece em /me/profiles: {mine}");
        request.GetProperty("kind").GetString().ShouldBe("new");
        request.GetProperty("name").GetString().ShouldBe("Podridão");
        request.GetProperty("type").GetString().ShouldBe("band");
        request.GetProperty("status").GetString().ShouldBe("pending");
        request.GetProperty("createdAtUtc").GetDateTimeOffset().ShouldBeInRange(
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));
        mine.ToString().ShouldNotContain(Contact, Case.Insensitive, "o contato do pedido só aparece na fila do admin");
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:IT-01")]
    [Trait("Kind", "Integration")]
    [MemberData(nameof(InvalidBodies))]
    public async Task AnInvalidRequestIs400WithTheField(string scenario, string field)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (fan, _) = await api.SignedInAsync(FanEmail, ct);
        using var __ = fan;
        var body = ProfilesRequests.Band("Podridão");
        switch (scenario)
        {
            case "nome curto":
                body["name"] = "P";
                break;
            case "cidade inexistente":
                body["city"] = "atlantida";
                break;
            case "estilo inexistente":
                body["styles"] = new[] { "sertanejo" };
                break;
            case "instagram http":
                body["links"] = new Dictionary<string, string?> { ["instagram"] = "http://instagram.com/x" };
                break;
            case "instagram de outro domínio":
                body["links"] = new Dictionary<string, string?> { ["instagram"] = "https://evil.com/x" };
                break;
            case "contato curto":
                body["contact"] = "ab";
                break;
            default:
                body["type"] = "tribute";
                break;
        }

        using var response = await fan.PostAsync(ProfilesRequests.Requests, body, ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "profiles.invalid_request", ct);
        problem.ShouldHaveFieldError(field);
        (await ProfilesDatabase.RequestsAsync(api.ConnectionString, ct)).ShouldBeEmpty($"{scenario}: nada gravado");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task ARepeatedRequestIs409RequestPending()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (fan, _) = await api.SignedInAsync(FanEmail, ct);
        using var __ = fan;
        await fan.RequestProfileAsync(ProfilesRequests.Band("Podridão"), ct);

        using var again = await fan.PostAsync(ProfilesRequests.Requests, ProfilesRequests.Band("Outra Banda"), ct);

        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "profiles.request_pending", ct);
        (await ProfilesDatabase.RequestsAsync(api.ConnectionString, ct)).Count.ShouldBe(1);
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task ANameAlreadyUsedInTheTypeIs409NameTaken()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (admin, _) = await api.SignedInAsync(ProfilesApiFactory.AdminEmail, ct);
        using var __ = admin;
        await admin.CreateOwnerlessAsync(ProfilesRequests.Ownerless("Podridão", "band"), ct);
        var (fan, _) = await api.SignedInAsync(FanEmail, ct);
        using var ___ = fan;

        using var response = await fan.PostAsync(ProfilesRequests.Requests, ProfilesRequests.Band("Podridão"), ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "profiles.name_taken", ct);
        (await ProfilesDatabase.RequestsAsync(api.ConnectionString, ct)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task WithoutSessionIs401AndWithoutCsrfIs400()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        using var anonymous = api.CreateProfilesClient();

        using (var response = await anonymous.PostAsync(ProfilesRequests.Requests, ProfilesRequests.Band("Podridão"), ct))
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
        }

        using (var response = await anonymous.GetAsync(ProfilesRequests.MyProfiles, ct))
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.required", ct);
        }

        var (fan, _) = await api.SignedInAsync(FanEmail, ct);
        using var __ = fan;
        using (var response = await fan.SendAsync(HttpMethod.Post, ProfilesRequests.Requests, ct, ProfilesRequests.Band("Podridão")))
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "security.csrf_invalid", ct);
        }

        (await ProfilesDatabase.RequestsAsync(api.ConnectionString, ct)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task MyProfilesStartsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var api = await FactoryAsync(ct);
        var (fan, _) = await api.SignedInAsync(FanEmail, ct);
        using var __ = fan;

        var mine = await fan.MyProfilesAsync(ct);

        mine.GetProperty("profiles").ValueKind.ShouldBe(JsonValueKind.Array);
        mine.GetProperty("profiles").GetArrayLength().ShouldBe(0);
        mine.GetProperty("requests").GetArrayLength().ShouldBe(0);
    }
}
