using Microsoft.Extensions.DependencyInjection;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Users.IntegrationEvents;
using Subsolo.Users.IntegrationTests.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0021 IT-07 (§7.6, herança da revisão da SPEC-0019) — duas <see cref="IPersonalDataSection"/> com o mesmo nome
/// fazem a API falhar na inicialização, nomeando a seção (uma seção do export não pode sobrescrever a outra em
/// silêncio); nomes distintos sobem normalmente.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PersonalDataSectionNamesTests(PostgresFixture postgres)
{
    private const string Duplicated = "secao-duplicada";

    /// <summary>H26-09 — prazo para o host ficar pronto (migrações e jobs da outbox/inbox).</summary>
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-07")]
    [Trait("Kind", "Integration")]
    public async Task TwoSectionsWithTheSameNameFailAtStartupNamingIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(
            database,
            configureServices: services =>
            {
                services.AddSingleton<IPersonalDataSection>(new NamedSection(Duplicated, "primeira"));
                services.AddSingleton<IPersonalDataSection>(new NamedSection(Duplicated, "segunda"));
            });

        var failure = Should.Throw<Exception>(() => api.CreateUsersClient().Dispose());

        Flatten(failure).ShouldContain(
            message => message.Contains(Duplicated, StringComparison.Ordinal),
            $"a falha não nomeia a seção repetida: {failure}");
    }

    /// <summary>
    /// SPEC-0030 (H26-09, herança do CI da SPEC-0026) — logo depois de criar o banco isolado, o <c>/health</c> pode
    /// responder 503 enquanto as migrações (outbox/inbox) terminam: espera o host ficar pronto (200) até um prazo, em vez
    /// de afirmar o 200 na primeira consulta.
    /// </summary>
    [Fact]
    [Trait("Spec", "SPEC-0021:IT-07")]
    [Trait("Spec", "SPEC-0030:H26-09")]
    [Trait("Kind", "Integration")]
    public async Task SectionsWithDistinctNamesStartNormally()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await UsersApiFactory.IsolatedDatabaseAsync(postgres.ConnectionString, ct);
        await using var api = new UsersApiFactory(
            database,
            configureServices: services =>
            {
                services.AddSingleton<IPersonalDataSection>(new NamedSection("secao-um", "primeira"));
                services.AddSingleton<IPersonalDataSection>(new NamedSection("secao-dois", "segunda"));
            });

        using var client = api.CreateUsersClient();
        var deadline = DateTime.UtcNow + HealthTimeout;
        int status;
        while (true)
        {
            using var health = await client.GetAsync("/health", ct);
            status = (int)health.StatusCode;
            if (health.IsSuccessStatusCode || DateTime.UtcNow >= deadline)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
        }

        status.ShouldBe(200, $"/health não ficou pronto em {HealthTimeout.TotalSeconds:F0} s (último status: {status})");
    }

    private static IEnumerable<string> Flatten(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current.Message;
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions.SelectMany(Flatten))
                {
                    yield return inner;
                }
            }
        }
    }

    private sealed class NamedSection(string name, string marker) : IPersonalDataSection
    {
        public string Name => name;

        public Task<object?> ExportAsync(Guid userId, CancellationToken ct) => Task.FromResult<object?>(new { marker });
    }
}
