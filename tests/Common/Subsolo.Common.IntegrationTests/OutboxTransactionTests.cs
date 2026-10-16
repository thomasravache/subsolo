using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Subsolo.Common.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Template.Domain;
using Subsolo.Modules.Template.Infrastructure;

namespace Subsolo.Common.IntegrationTests;

/// <summary>
/// SPEC-0007 IT-01 — os eventos de domínio de uma entidade vão para a outbox do módulo na mesma transação do
/// <c>SaveChanges</c>: visíveis só depois do commit e descartados no rollback. A linha da outbox é localizada pelo
/// conteúdo (o nome único do item), sem depender dos nomes das colunas.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class OutboxTransactionTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("Spec", "SPEC-0007:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task DomainEventIsWrittenToOutboxInTheSameTransaction()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ModulesHost.CreateAsync(postgres, ct);
        await host.MigrateAsync<TemplateDbContext>(ct);
        var token = $"item-{Guid.NewGuid():N}";

        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
        await using (var transaction = await context.Database.BeginTransactionAsync(ct))
        {
            context.Items.Add(TemplateItem.Create(token));
            await context.SaveChangesAsync(ct);

            (await CountOutboxInsideAsync(context, token, ct)).ShouldBe(1, "a outbox não recebeu o evento na transação");
            (await CountOutboxOutsideAsync(host.ConnectionString, token, ct)).ShouldBe(0, "a outbox foi gravada fora da transação");

            await transaction.CommitAsync(ct);
        }

        (await CountOutboxOutsideAsync(host.ConnectionString, token, ct)).ShouldBe(1);
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task RolledBackTransactionWritesNothingToOutbox()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ModulesHost.CreateAsync(postgres, ct);
        await host.MigrateAsync<TemplateDbContext>(ct);
        var token = $"item-{Guid.NewGuid():N}";

        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
        await using (var transaction = await context.Database.BeginTransactionAsync(ct))
        {
            context.Items.Add(TemplateItem.Create(token));
            await context.SaveChangesAsync(ct);
            (await CountOutboxInsideAsync(context, token, ct)).ShouldBe(1, "a outbox não recebeu o evento na transação");

            await transaction.RollbackAsync(ct);
        }

        (await CountOutboxOutsideAsync(host.ConnectionString, token, ct)).ShouldBe(0);
        (await CountAsync(host.ConnectionString, "template.items", token, ct)).ShouldBe(0);
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task SaveChangesWithoutExplicitTransactionWritesEntityAndOutboxTogether()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ModulesHost.CreateAsync(postgres, ct);
        await host.MigrateAsync<TemplateDbContext>(ct);
        var token = $"item-{Guid.NewGuid():N}";

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            var item = TemplateItem.Create(token);
            context.Items.Add(item);
            await context.SaveChangesAsync(ct);
            item.DomainEvents.ShouldBeEmpty("os eventos gravados na outbox continuam pendentes na entidade");
        }

        (await CountAsync(host.ConnectionString, "template.items", token, ct)).ShouldBe(1);
        (await CountOutboxOutsideAsync(host.ConnectionString, token, ct)).ShouldBe(1);
    }

    /// <summary>Conta, pela conexão (e transação) do DbContext, as linhas da outbox que mencionam o token.</summary>
    private static Task<int> CountOutboxInsideAsync(TemplateDbContext context, string token, CancellationToken ct) =>
        context.Database
            .SqlQuery<int>($"select count(*)::int as \"Value\" from template.outbox_messages o where row_to_json(o)::text like '%' || {token} || '%'")
            .SingleAsync(ct);

    /// <summary>Conta, por outra conexão (só vê o que foi confirmado), as linhas da outbox que mencionam o token.</summary>
    private static Task<int> CountOutboxOutsideAsync(string connectionString, string token, CancellationToken ct) =>
        CountAsync(connectionString, "template.outbox_messages", token, ct);

    private static async Task<int> CountAsync(string connectionString, string table, string token, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        // Tabela vem de constantes deste teste; o token vai como parâmetro.
#pragma warning disable CA2100 // SQL montado só com valores gerados ou constantes do próprio teste
        await using var command = new NpgsqlCommand(
            $"select count(*)::int from {table} t where row_to_json(t)::text like '%' || @token || '%'", connection);
#pragma warning restore CA2100
        command.Parameters.AddWithValue("token", token);
        return (int)(await command.ExecuteScalarAsync(ct))!;
    }
}
