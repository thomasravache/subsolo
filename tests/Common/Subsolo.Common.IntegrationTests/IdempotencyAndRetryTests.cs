using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Application.EventBus;
using Subsolo.Common.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Template.Application;
using Subsolo.Modules.Template.Domain;
using Subsolo.Modules.Template.Infrastructure;
using Subsolo.Modules.Template.IntegrationEvents;
using Subsolo.Modules.TemplateB.Application;

namespace Subsolo.Common.IntegrationTests;

/// <summary>
/// SPEC-0007 IT-04 — inbox idempotente (o mesmo evento entregue duas vezes é processado uma vez) e retry da
/// outbox (um handler que falha é executado de novo depois, até dar certo, e nunca mais depois do sucesso).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class IdempotencyAndRetryTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(1);

    /// <summary>Passo maior no teste de retry: o backoff pode somar dezenas de segundos simulados.</summary>
    private static readonly TimeSpan RetryStep = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Spec", "SPEC-0007:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task SameEventDeliveredTwiceIsHandledOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ModulesHost.CreateAsync(postgres, ct);
        await host.MigrateAllAsync(ct);
        await host.StartAsync(ct);
        var probe = host.Services.GetRequiredService<TemplateBProbe>();
        var integrationEvent = new TemplateItemCreatedIntegrationEvent(
            Guid.NewGuid(), host.Clock.GetUtcNow().UtcDateTime, Guid.NewGuid(), $"item-{Guid.NewGuid():N}");

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await eventBus.PublishAsync(integrationEvent, ct);
            await eventBus.PublishAsync(integrationEvent, ct);
        }

        var elapsed = await host.AdvanceUntilAsync(
            () => probe.Received.Any(e => e.Id == integrationEvent.Id), TimeSpan.FromSeconds(10), Step, ct);
        elapsed.ShouldNotBeNull("o handler do TemplateB nunca recebeu o evento");

        // Mais ciclos dos jobs (cada passo espera as execuções devidas): uma segunda execução apareceria aqui.
        var inboxRunsBefore = host.CompletedExecutions("ProcessInbox");
        await host.AdvanceAsync(TimeSpan.FromSeconds(10), Step, ct);
        host.CompletedExecutions("ProcessInbox").ShouldBeGreaterThan(inboxRunsBefore, "o job da inbox não rodou depois da entrega");

        probe.Received.Count(e => e.Id == integrationEvent.Id).ShouldBe(1, "a inbox processou o mesmo evento mais de uma vez");
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task FailingOutboxHandlerIsRetriedUntilItSucceedsAndThenNeverAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ModulesHost.CreateAsync(postgres, ct);
        await host.MigrateAllAsync(ct);
        var faults = host.Services.GetRequiredService<TemplateFaults>();
        var probe = host.Services.GetRequiredService<TemplateBProbe>();
        faults.FailNext(2);
        await host.StartAsync(ct);
        var token = $"item-{Guid.NewGuid():N}";

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            context.Items.Add(TemplateItem.Create(token));
            await context.SaveChangesAsync(ct);
        }

        var elapsed = await host.AdvanceUntilAsync(
            () => probe.Received.Any(e => e.Name == token), TimeSpan.FromMinutes(3), RetryStep, ct);
        elapsed.ShouldNotBeNull("depois de 2 falhas, a outbox não tentou de novo até o evento ser entregue");

        // Cada passo espera as execuções devidas dos jobs: sem nova tentativa aqui, ela nunca aconteceu.
        var outboxRunsBefore = host.CompletedExecutions("ProcessOutbox");
        await host.AdvanceAsync(TimeSpan.FromSeconds(30), RetryStep, ct);
        host.CompletedExecutions("ProcessOutbox").ShouldBeGreaterThan(outboxRunsBefore, "o job da outbox não rodou depois do sucesso");

        faults.Attempts.ShouldBe(3, "esperadas 2 falhas e 1 sucesso, sem novas execuções depois do sucesso");
        probe.Received.Count(e => e.Name == token).ShouldBe(1);
    }
}
