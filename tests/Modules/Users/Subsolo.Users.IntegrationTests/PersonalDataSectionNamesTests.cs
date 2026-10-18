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

    [Fact]
    [Trait("Spec", "SPEC-0021:IT-07")]
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
        using var health = await client.GetAsync("/health", ct);

        health.IsSuccessStatusCode.ShouldBeTrue($"/health: {(int)health.StatusCode}");
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
