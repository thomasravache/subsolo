using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Subsolo.Common.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Template.Application;
using Subsolo.Modules.Template.Domain;
using Subsolo.Modules.Template.Infrastructure;
using Subsolo.Modules.TemplateB.Application;

namespace Subsolo.Common.IntegrationTests;

/// <summary>
/// SPEC-0013 IT-07 — um <see cref="SaveChangesInterceptor"/> faz falhar, uma única vez, a gravação do estado de uma
/// mensagem da outbox do Template (o <c>SaveChanges</c> que marca a mensagem como processada, depois que o handler
/// rodou e o registro de consumer foi gravado). A mensagem seguinte do mesmo lote é processada na mesma execução; a que
/// falhou volta a ser lida numa execução seguinte e é marcada como processada sem rodar o handler de novo.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class StateSaveFailureTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(1);

    [Fact]
    [Trait("Spec", "SPEC-0013:IT-07")]
    [Trait("Kind", "Integration")]
    public async Task FailedStateSaveDoesNotAbortTheBatchAndIsRetriedWithoutRunningTheHandlerAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var failOnce = new FailFirstOutboxStateSave();
        await using var host = await ModulesHost.CreateAsync(
            postgres,
            services => services.ConfigureDbContext<TemplateDbContext>(options => options.AddInterceptors(failOnce)),
            ct);
        await host.MigrateAllAsync(ct);
        var faults = host.Services.GetRequiredService<TemplateFaults>();
        var probe = host.Services.GetRequiredService<TemplateBProbe>();
        string[] tokens = [$"item-a-{Guid.NewGuid():N}", $"item-b-{Guid.NewGuid():N}"];

        // As duas mensagens já estão na outbox quando o job roda pela primeira vez: entram no mesmo lote.
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            foreach (var token in tokens)
            {
                context.Items.Add(TemplateItem.Create(token));
            }

            await context.SaveChangesAsync(ct);
        }

        await host.StartAsync(ct);

        failOnce.FailedMessageId.ShouldNotBeNull("o interceptor não chegou a falhar a gravação do estado de nenhuma mensagem");
        var states = await OutboxStatesAsync(host.ConnectionString, ct);
        states.Count.ShouldBe(2, "esperadas as duas mensagens na outbox do Template");
        states[failOnce.FailedMessageId.Value].ShouldBeFalse("a mensagem cuja gravação de estado falhou não pode constar como processada");
        states.Where(s => s.Key != failOnce.FailedMessageId.Value).ShouldAllBe(
            s => s.Value, "a falha ao gravar o estado de uma mensagem abortou o lote: a mensagem seguinte não foi processada");

        var reprocessed = await host.AdvanceUntilAsync(
            () => OutboxStatesAsync(host.ConnectionString, ct).GetAwaiter().GetResult().Values.All(processed => processed),
            TimeSpan.FromSeconds(30),
            Step,
            ct);
        reprocessed.ShouldNotBeNull("a mensagem cuja gravação de estado falhou não foi processada numa execução seguinte");

        // Mais ciclos: uma nova execução do handler (ou do TemplateB) apareceria aqui.
        await host.AdvanceAsync(TimeSpan.FromSeconds(10), Step, ct);
        faults.Attempts.ShouldBe(2, "o handler da outbox rodou de novo para a mensagem que já tinha o registro de consumer");
        foreach (var token in tokens)
        {
            probe.Received.Count(e => e.Name == token).ShouldBe(1, $"o TemplateB deveria receber {token} uma única vez");
        }
    }

    /// <summary>processed_on_utc preenchido, por id, das mensagens da outbox do Template.</summary>
    private static async Task<Dictionary<Guid, bool>> OutboxStatesAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "select id, processed_on_utc is not null from template.outbox_messages", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var states = new Dictionary<Guid, bool>();
        while (await reader.ReadAsync(ct))
        {
            states[reader.GetGuid(0)] = reader.GetBoolean(1);
        }

        return states;
    }

    /// <summary>
    /// Falha o primeiro <c>SaveChanges</c> que grava o estado (linha modificada) de uma mensagem da outbox; os
    /// demais seguem normalmente. Os inserts (eventos novos, registros de consumer) nunca falham.
    /// </summary>
    private sealed class FailFirstOutboxStateSave : SaveChangesInterceptor
    {
        private readonly Lock _gate = new();

        public Guid? FailedMessageId { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventData);
            var modified = eventData.Context?.ChangeTracker.Entries()
                .FirstOrDefault(entry => entry.State == EntityState.Modified && entry.Metadata.GetTableName() == "outbox_messages");
            if (modified is not null)
            {
                lock (_gate)
                {
                    if (FailedMessageId is null)
                    {
                        FailedMessageId = (Guid)modified.Property("Id").CurrentValue!;
                        throw new InvalidOperationException("Falha injetada pelo teste ao gravar o estado da mensagem (IT-07).");
                    }
                }
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
