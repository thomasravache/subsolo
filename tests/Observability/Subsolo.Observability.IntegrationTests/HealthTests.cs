using System.Net;
using System.Text.Json;
using Subsolo.Common.Infrastructure;
using Subsolo.Observability.IntegrationTests.Infrastructure;
using Testcontainers.PostgreSql;

namespace Subsolo.Observability.IntegrationTests;

/// <summary>
/// SPEC-0009 IT-05 — <c>GET /health</c> do Subsolo.Api com PostgreSQL real (container próprio do teste):
/// <c>200 {status: Healthy}</c> com o banco ok e a outbox rodando; com o container do PostgreSQL pausado,
/// <c>503 {status: Unhealthy, entries: {postgres, outbox}}</c> com a entrada <c>postgres</c> Unhealthy; ao retomar,
/// volta a 200.
/// </summary>
public sealed class HealthTests : IAsyncLifetime
{
    /// <summary>Folga para o primeiro ciclo da outbox, a detecção do banco pausado e a reconexão.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(90);

    private PostgreSqlContainer? _postgres;

    public async ValueTask InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:18.6").Build();
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
    [Trait("Spec", "SPEC-0009:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task HealthFollowsThePostgresState()
    {
        var ct = TestContext.Current.CancellationToken;
        var postgres = _postgres.ShouldNotBeNull();
        var database = await ObservedApiFactory.IsolatedDatabaseAsync(postgres.GetConnectionString(), ct);
        await using var api = new ObservedApiFactory(database);
        using var client = api.CreateClient();
        client.Timeout = Patience;

        var healthy = await WaitForAsync(client, HttpStatusCode.OK, ct);
        healthy.Status.ShouldBe("Healthy");

        await postgres.PauseAsync(ct);
        Health unhealthy;
        try
        {
            unhealthy = await WaitForAsync(client, HttpStatusCode.ServiceUnavailable, ct);
        }
        finally
        {
            await postgres.UnpauseAsync(ct);
        }

        unhealthy.Status.ShouldBe("Unhealthy");
        unhealthy.Entries.ShouldContainKey("postgres", "o /health não traz a entrada postgres");
        unhealthy.Entries.ShouldContainKey("outbox", "o /health não traz a entrada outbox");
        unhealthy.Entries["postgres"].ShouldBe("Unhealthy", "com o PostgreSQL pausado, a entrada postgres não ficou Unhealthy");

        var recovered = await WaitForAsync(client, HttpStatusCode.OK, ct);
        recovered.Status.ShouldBe("Healthy");
    }

    /// <summary>Chama <c>/health</c> até responder <paramref name="expected"/> (ou estourar <see cref="Patience"/>).</summary>
    private static async Task<Health> WaitForAsync(HttpClient client, HttpStatusCode expected, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + Patience;
        var last = "nenhuma resposta";
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            last = $"{(int)response.StatusCode} {body}";
            if (response.StatusCode == expected)
            {
                return Parse(body);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }

        throw new ShouldAssertException($"/health não respondeu {(int)expected} em {Patience.TotalSeconds} s; última resposta: {last}");
    }

    /// <summary>Lê <c>{status, entries: {nome: {status}}}</c>; corpo fora desse formato falha o teste.</summary>
    private static Health Parse(string body)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException exception)
        {
            throw new ShouldAssertException($"/health não respondeu JSON {{status, entries}}: \"{body}\"", exception);
        }

        using (document)
        {
            var root = document.RootElement;
            root.ValueKind.ShouldBe(JsonValueKind.Object, $"/health: \"{body}\"");
            root.TryGetProperty("status", out var status).ShouldBeTrue($"/health sem \"status\": {body}");
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetProperty("entries", out var entriesElement) && entriesElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in entriesElement.EnumerateObject())
                {
                    entries[entry.Name] = entry.Value.ValueKind == JsonValueKind.Object && entry.Value.TryGetProperty("status", out var s)
                        ? s.GetString() ?? string.Empty
                        : entry.Value.ToString();
                }
            }

            return new Health(status.GetString() ?? string.Empty, entries);
        }
    }

    private sealed record Health(string Status, IReadOnlyDictionary<string, string> Entries);
}
