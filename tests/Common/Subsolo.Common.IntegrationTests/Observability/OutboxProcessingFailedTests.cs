using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Subsolo.Common.Application.EventBus;
using Subsolo.Common.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Template.Application;
using Subsolo.Modules.Template.Domain;
using Subsolo.Modules.Template.Infrastructure;
using Subsolo.Modules.Template.IntegrationEvents;

namespace Subsolo.Common.IntegrationTests.Observability;

/// <summary>
/// SPEC-0009 UT-05 (contrato v2) — o evento <c>OutboxProcessingFailed</c> (pelo <c>EventName</c>, base do alerta
/// "Falha de job" do Seq) é emitido, com a exceção, quando uma mensagem da outbox ou da inbox falha ao ser processada
/// e quando o job inteiro falha. Usa os jobs reais do Quartz (<see cref="ModulesHost"/>, PostgreSQL real): o
/// processamento de mensagens depende do EF Core e não tem dublê em memória no projeto.
/// <para>
/// Inbox: o handler do TemplateB (<c>tests/Common/Modules</c>, fora dos <c>touches</c> da spec) não tem injeção de
/// falha, então a mensagem da inbox falha por ter o tipo trocado para um desconhecido — o mesmo caminho de falha
/// por mensagem (<c>catch</c> do <c>ProcessMessagesJob</c>) que um handler que lança.
/// </para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class OutboxProcessingFailedTests(PostgresFixture postgres)
{
    private const string EventName = "OutboxProcessingFailed";
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(1);

    [Fact]
    [Trait("Spec", "SPEC-0009:UT-05")]
    [Trait("Kind", "Integration")]
    public async Task OutboxMessageWhoseHandlerAlwaysThrowsEmitsTheEventWithTheException()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ModulesHost.CreateAsync(postgres, ct);
        await host.MigrateAllAsync(ct);
        var logs = CapturedLogs.AttachTo(host.Services);
        host.Services.GetRequiredService<TemplateFaults>().FailNext(int.MaxValue);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            context.Items.Add(TemplateItem.Create($"item-{Guid.NewGuid():N}"));
            await context.SaveChangesAsync(ct);
        }

        await host.StartAsync(ct);
        await host.AdvanceUntilAsync(
            () => host.Services.GetRequiredService<TemplateFaults>().Attempts > 0, TimeSpan.FromSeconds(10), Step, ct);
        host.Services.GetRequiredService<TemplateFaults>().Attempts.ShouldBeGreaterThan(0, "a outbox não chamou o handler");

        var failed = Failures(logs);
        failed.ShouldContain(
            log => log.Exception is InvalidOperationException &&
                   log.Exception.Message.StartsWith("Falha injetada pelo teste", StringComparison.Ordinal),
            $"a falha do handler da outbox não gerou um log com EventName={EventName} e a exceção do handler. " +
            $"Logs com a exceção: {Describe(logs, "Falha injetada pelo teste")}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0009:UT-05")]
    [Trait("Kind", "Integration")]
    public async Task InboxMessageThatFailsToBeProcessedEmitsTheEventWithTheException()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ModulesHost.CreateAsync(postgres, ct);
        await host.MigrateAllAsync(ct);
        var logs = CapturedLogs.AttachTo(host.Services);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IEventBus>().PublishAsync(
                new TemplateItemCreatedIntegrationEvent(
                    Guid.NewGuid(), host.Clock.GetUtcNow().UtcDateTime, Guid.NewGuid(), $"item-{Guid.NewGuid():N}"),
                ct);
        }

        var changed = await ExecuteAsync(
            host, "update template_b.inbox_messages set type = 'Subsolo.Tests.EventoDesconhecido'", ct);
        changed.ShouldBe(1, "a mensagem não chegou à inbox do TemplateB");

        await host.StartAsync(ct);
        await host.AdvanceAsync(TimeSpan.FromSeconds(3), Step, ct);

        var failed = Failures(logs);
        failed.ShouldContain(
            log => log.Exception is InvalidOperationException &&
                   log.Exception.Message.Contains("Subsolo.Tests.EventoDesconhecido", StringComparison.Ordinal),
            $"a falha da mensagem da inbox não gerou um log com EventName={EventName} e a exceção. " +
            $"Logs com a exceção: {Describe(logs, "Subsolo.Tests.EventoDesconhecido")}");
    }

    [Theory]
    [Trait("Spec", "SPEC-0009:UT-05")]
    [Trait("Kind", "Integration")]
    [InlineData("template.outbox_messages", "TemplateDbContext.ProcessOutbox")]
    [InlineData("template_b.inbox_messages", "TemplateBDbContext.ProcessInbox")]
    public async Task JobThatFailsAsAWholeEmitsTheEventWithTheException(string table, string job)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ModulesHost.CreateAsync(postgres, ct);
        await host.MigrateAllAsync(ct);
        var logs = CapturedLogs.AttachTo(host.Services);

        // Sem a tabela, a leitura das mensagens pendentes falha: o job inteiro lança (só este job).
        await ExecuteAsync(host, $"alter table {table} rename to {table[(table.IndexOf('.') + 1)..]}_fora", ct);

        await host.StartAsync(ct);
        await host.AdvanceAsync(TimeSpan.FromSeconds(2), Step, ct);

        Failures(logs).ShouldContain(
            log => log.Exception != null && Chain(log.Exception).Any(e => e is PostgresException),
            $"a falha do job {job} inteiro não gerou um log com EventName={EventName} e a exceção. " +
            $"Logs de erro: {string.Join(" | ", logs.All.Where(l => l.Exception != null).Select(l => $"[{l.EventId.Name}] {l.Category}: {l.Message}"))}");
    }

    private static List<CapturedLog> Failures(CapturedLogs logs) =>
        [.. logs.All.Where(log => log.EventId.Name == EventName)];

    private static string Describe(CapturedLogs logs, string exceptionText) =>
        string.Join(" | ", logs.All
            .Where(log => log.Exception?.ToString().Contains(exceptionText, StringComparison.Ordinal) == true)
            .Select(log => $"[EventName={log.EventId.Name ?? "(nenhum)"}] {log.Category}: {log.Message}")
            .DefaultIfEmpty("(nenhum)"));

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    private static async Task<int> ExecuteAsync(ModulesHost host, string sql, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(host.ConnectionString);
        await connection.OpenAsync(ct);
#pragma warning disable CA2100 // SQL fixo do teste, sem entrada externa
        await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
        return await command.ExecuteNonQueryAsync(ct);
    }
}
