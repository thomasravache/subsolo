using System.Net;
using System.Text.Json;
using Npgsql;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.IntegrationTests.Common;
using Subsolo.Users.IntegrationTests.Infrastructure;
using Testcontainers.PostgreSql;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0014 IT-07 — com o container do PostgreSQL (próprio do teste) pausado, <c>/me</c> com cookie de sessão
/// responde 503 em ProblemDetails, sem detalhes da exceção — nunca usuário anônimo silencioso nem 500. Fora de
/// Development (Production), com as migrações aplicadas pelo teste.
/// </summary>
public sealed class DatabaseDownTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;

    public async ValueTask InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder(PostgresFixture.Image).Build();
        await _postgres.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0014:IT-07")]
    [Trait("Kind", "Integration")]
    public async Task SessionValidationWithoutDatabaseReturns503ProblemDetailsWithoutDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        var postgres = _postgres.ShouldNotBeNull();
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.GetConnectionString(), ct);
        // Prazos curtos: com o banco pausado, a conexão e o comando desistem logo.
        var connectionString = new NpgsqlConnectionStringBuilder(database) { Timeout = 3, CommandTimeout = 3 }.ConnectionString;
        await using var api = new UsersApiFactory(connectionString, environment: "Production");
        await api.Services.MigrateModulesAsync(ct);
        using var client = api.CreateUsersClient();
        var signedIn = await client.SignInAsync("fulano@subsolo.test", ct);

        await postgres.PauseAsync(ct);
        HttpResponseMessage response;
        string body;
        try
        {
            response = await client.GetAsync(MeEndpointTests.MeRoute, ct, sessionCookie: signedIn.SessionCookie);
            body = await response.Content.ReadAsStringAsync(ct);
        }
        finally
        {
            await postgres.UnpauseAsync(ct);
        }

        using (response)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable, UsersApiClient.Excerpt(body));
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json", body);
            using var document = JsonDocument.Parse(body);
            document.RootElement.GetProperty("status").GetInt32().ShouldBe(503, body);
            body.ShouldNotContain("Npgsql", Case.Insensitive, "detalhe da exceção vazou");
            body.ShouldNotContain("Exception", Case.Insensitive, "detalhe da exceção vazou");
            body.ShouldNotContain("stack", Case.Insensitive, "o stack trace vazou");
            body.ShouldNotContain(" at ", Case.Sensitive, "o stack trace vazou");
            body.ShouldNotContain("fulano@subsolo.test", Case.Insensitive, "dado do usuário vazou");
        }
    }
}
