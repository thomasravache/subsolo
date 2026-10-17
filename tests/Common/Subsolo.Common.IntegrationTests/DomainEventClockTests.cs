using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Subsolo.Common.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Template.Domain;
using Subsolo.Modules.Template.Infrastructure;

namespace Subsolo.Common.IntegrationTests;

/// <summary>
/// SPEC-0013 UT-05 — o <c>OccurredOnUtc</c> do evento de domínio gravado na outbox vem do <see cref="TimeProvider"/>
/// da aplicação, não do relógio do sistema (<c>DateTime.UtcNow</c>). O relógio falso é levado para um instante
/// distante do real (um ano à frente), de modo que os dois relógios não se confundam. A gravação na outbox exige o
/// <c>SaveChanges</c> real (PostgreSQL), por isso o teste mora no projeto de integração.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class DomainEventClockTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0013:UT-05")]
    [Trait("Kind", "Integration")]
    public async Task DomainEventOccurredOnUtcComesFromTheTimeProvider()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ModulesHost.CreateAsync(postgres, ct);
        await host.MigrateAsync<TemplateDbContext>(ct);
        host.Clock.Advance(TimeSpan.FromDays(365));
        var expected = host.Clock.GetUtcNow().UtcDateTime;
        var token = $"item-{Guid.NewGuid():N}";

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            context.Items.Add(TemplateItem.Create(token));
            await context.SaveChangesAsync(ct);
        }

        var stored = await StoredOccurredOnUtcAsync(host.ConnectionString, token, ct);
        stored.ShouldNotBeNull("o evento de domínio não foi gravado na outbox com OccurredOnUtc no conteúdo");
        var occurredOnUtc = DateTime.Parse(stored, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        occurredOnUtc.ShouldBe(expected,
            $"DomainEvent.OccurredOnUtc deve vir do TimeProvider ({expected:O}), não do relógio do sistema; gravado: {stored}");
    }

    /// <summary><c>OccurredOnUtc</c> do evento serializado na coluna <c>content</c> da outbox do Template.</summary>
    private static async Task<string?> StoredOccurredOnUtcAsync(string connectionString, string token, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "select content->>'OccurredOnUtc' from template.outbox_messages where content->>'Name' = @token", connection);
        command.Parameters.AddWithValue("token", token);
        return (string?)await command.ExecuteScalarAsync(ct);
    }
}
