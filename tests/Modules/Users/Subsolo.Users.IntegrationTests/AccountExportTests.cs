using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Users.Infrastructure;
using Subsolo.Modules.Users.IntegrationEvents;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0019 IT-02 — com o PostgreSQL real, um usuário com 50 sessões (e outro usuário) e uma
/// <see cref="IPersonalDataSection"/> de teste registrada no host, <c>GET /api/v1/me/export</c> responde 200 com
/// <c>Content-Disposition: attachment; filename="subsolo-meus-dados.json"</c>, <c>no-store</c> e o corpo do §6 (conta,
/// as 50 sessões sem <c>id_hash</c> nem valor de cookie, a seção de teste), sem nada do outro usuário; p95 de 20
/// chamadas ≤ 1 s. §7.6 (herança da SPEC-0016) — o teto absoluto das sessões exportadas é o
/// <see cref="UsersModule.SessionAbsoluteLifetime"/> exposto pelo módulo, que é também a validade do cookie no host.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AccountExportTests(PostgresFixture postgres)
{
    private const string Email = "fulano@subsolo.test";
    private const string OtherEmail = "outra.pessoa@subsolo.test";
    private const int SessionCount = 50;
    private const int Calls = 20;

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(1);

    private static readonly Dictionary<string, string> HighRateLimit = new(StringComparer.Ordinal)
    {
        ["Security:RateLimiting:default:PermitLimit"] = "10000",
        ["Security:RateLimiting:default:Window"] = "00:01:00",
    };

    [Fact]
    [Trait("Spec", "SPEC-0019:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task ExportIsAnAttachmentWithTheAccountTheSessionsAndTheSections()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(
            database,
            settings: HighRateLimit,
            configureServices: services => services.AddSingleton<IPersonalDataSection, TestSection>());
        using var other = api.CreateUsersClient();
        var otherSignedIn = await other.SignInAsync(OtherEmail, ct);
        using var client = api.CreateUsersClient();
        var cookies = new List<string>();
        SignedIn signedIn = null!;
        for (var i = 0; i < SessionCount; i++)
        {
            signedIn = await client.SignInAsync(Email, ct);
            cookies.Add(signedIn.SessionCookie);
        }

        await UsersDatabase.SetDisplayNameAsync(database, signedIn.UserId, "Caio Grind", ct);
        var sessionRows = (await UsersDatabase.SessionsAsync(database, ct)).ToList();
        sessionRows.Count(row => row.UserId == signedIn.UserId).ShouldBe(SessionCount);

        using var response = await client.ExportAsync(ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(body));
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json", body);
        response.ShouldBeNoStore();
        var disposition = response.Content.Headers.ContentDisposition;
        disposition.ShouldNotBeNull("sem Content-Disposition: o navegador não baixa o arquivo");
        disposition.DispositionType.ShouldBe("attachment");
        disposition.FileName.ShouldNotBeNull("Content-Disposition sem filename").Trim('"').ShouldBe(AccountRequests.ExportFileName);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Names(root).ShouldBe(["account", "exportedAtUtc", "sections", "sessions"], body[..Math.Min(body.Length, 600)]);
        root.GetProperty("exportedAtUtc").GetDateTimeOffset().ShouldBeInRange(
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));

        var account = root.GetProperty("account");
        Names(account).ShouldBe(["createdAtUtc", "displayName", "email", "id", "roles"]);
        account.GetProperty("id").GetGuid().ShouldBe(signedIn.UserId);
        account.GetProperty("email").GetString().ShouldBe(Email);
        account.GetProperty("displayName").GetString().ShouldBe("Caio Grind");
        account.GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ShouldBe(["fan"]);
        account.GetProperty("createdAtUtc").GetDateTimeOffset().ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);

        var sessions = root.GetProperty("sessions").EnumerateArray().ToList();
        sessions.Count.ShouldBe(SessionCount, "as 50 sessões do usuário (nenhuma do outro)");
        foreach (var session in sessions)
        {
            Names(session).ShouldBe(
                ["absoluteExpiresAtUtc", "createdAtUtc", "expiresAtUtc", "lastSeenAtUtc", "method", "revokedAtUtc"]);
            session.GetProperty("method").GetString().ShouldBe("magic_link");
            session.GetProperty("revokedAtUtc").ValueKind.ShouldBe(JsonValueKind.Null);
        }

        var sections = root.GetProperty("sections");
        Names(sections).ShouldBe([TestSection.SectionName]);
        sections.GetProperty(TestSection.SectionName).GetProperty("marker").GetString().ShouldBe(TestSection.Marker);
        sections.GetProperty(TestSection.SectionName).GetProperty("userId").GetGuid().ShouldBe(signedIn.UserId);

        body.ShouldNotContain("id_hash", Case.Insensitive);
        body.ShouldNotContain("idHash", Case.Insensitive);
        foreach (var row in sessionRows)
        {
            body.ShouldNotContain(row.IdHash.TrimStart('\\', 'x'), Case.Insensitive, "hash de sessão no export");
        }

        foreach (var cookie in cookies.Append(otherSignedIn.SessionCookie))
        {
            body.ShouldNotContain(cookie, Case.Sensitive, "valor de cookie no export");
        }

        body.ShouldNotContain(OtherEmail, Case.Insensitive, "e-mail de outro usuário no export");
        body.ShouldNotContain(otherSignedIn.UserId.ToString(), Case.Insensitive, "id de outro usuário no export");
    }

    [Fact]
    [Trait("Spec", "SPEC-0019:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task ExportOfFiftySessionsIsUnderOneSecondAtP95()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(
            database,
            settings: HighRateLimit,
            configureServices: services => services.AddSingleton<IPersonalDataSection, TestSection>());
        using var client = api.CreateUsersClient();
        for (var i = 0; i < SessionCount; i++)
        {
            await client.SignInAsync(Email, ct);
        }

        using (var warmUp = await client.ExportAsync(ct))
        {
            warmUp.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(await warmUp.Content.ReadAsStringAsync(ct)));
        }

        var durations = new List<TimeSpan>();
        for (var i = 0; i < Calls; i++)
        {
            var (response, elapsed) = await AccountRequests.TimedAsync(() => client.ExportAsync(ct));
            using (response)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            durations.Add(elapsed);
        }

        AccountRequests.Percentile(durations, 95).ShouldBeLessThanOrEqualTo(
            Budget, $"p95 do export (ms): {string.Join(", ", durations.Select(d => (int)d.TotalMilliseconds))}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0019:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task ExportedAbsoluteExpiryAndTheCookieLifetimeComeFromUsersModule()
    {
        var ct = TestContext.Current.CancellationToken;
        UsersModule.SessionAbsoluteLifetime.ShouldBe(TimeSpan.FromDays(90));
        var cookieLifetime = typeof(Program).Assembly
            .GetType("Subsolo.Api.Security.SecurityExtensions", throwOnError: true)!
            .GetField("SessionCookieLifetime", BindingFlags.Public | BindingFlags.Static)
            .ShouldNotBeNull("SecurityExtensions.SessionCookieLifetime não existe")
            .GetValue(null);
        cookieLifetime.ShouldBe(UsersModule.SessionAbsoluteLifetime, "o cookie vale o teto absoluto exposto pelo módulo");

        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(database);
        using var client = api.CreateUsersClient();
        await client.SignInAsync(Email, ct);

        using var response = await client.ExportAsync(ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, UsersApiClient.Excerpt(body));
        using var document = JsonDocument.Parse(body);
        var session = document.RootElement.GetProperty("sessions").EnumerateArray().ShouldHaveSingleItem();
        (session.GetProperty("absoluteExpiresAtUtc").GetDateTimeOffset() - session.GetProperty("createdAtUtc").GetDateTimeOffset())
            .ShouldBe(UsersModule.SessionAbsoluteLifetime);
    }

    private static string[] Names(JsonElement element) =>
        [.. element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)];

    /// <summary>Dublê de <see cref="IPersonalDataSection"/> registrado só neste host de teste.</summary>
    private sealed class TestSection : IPersonalDataSection
    {
        public const string SectionName = "teste";

        public const string Marker = "secao-de-teste";

        public string Name => SectionName;

        public Task<object?> ExportAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<object?>(new { marker = Marker, userId });
    }
}
