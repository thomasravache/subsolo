using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Template.Application;
using Subsolo.Modules.Template.Domain;
using Subsolo.Modules.Template.Infrastructure;
using Subsolo.Modules.TemplateB.Application;

namespace Subsolo.Common.IntegrationTests;

/// <summary>
/// SPEC-0007 IT-08 — o handler do evento de domínio do Template falha logo depois de publicar o evento de integração
/// na 1ª tentativa; a outbox tenta de novo e publica outra vez. Como o evento de integração reaproveita o
/// <c>Id</c> (Guid v7) do evento de domínio, a inbox do TemplateB descarta a segunda publicação e o handler do
/// TemplateB executa uma única vez.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class RetryWithoutDuplicationTests(PostgresFixture postgres)
{
    /// <summary>Passo maior: o backoff da 1ª falha é de alguns segundos simulados.</summary>
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(2);

    [Fact]
    [Trait("Spec", "SPEC-0007:IT-08")]
    [Trait("Kind", "Integration")]
    public async Task HandlerFailingRightAfterPublishIsRetriedAndModuleBHandlesTheEventOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ModulesHost.CreateAsync(postgres, ct);
        await host.MigrateAllAsync(ct);
        var faults = host.Services.GetRequiredService<TemplateFaults>();
        var probe = host.Services.GetRequiredService<TemplateBProbe>();
        faults.FailAfterPublishNext(1);
        await host.StartAsync(ct);
        var token = $"item-{Guid.NewGuid():N}";
        Guid domainEventId;

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            var item = TemplateItem.Create(token);
            domainEventId = item.DomainEvents.ShouldHaveSingleItem().Id;
            context.Items.Add(item);
            await context.SaveChangesAsync(ct);
        }

        domainEventId.Version.ShouldBe(7, "o Id do evento de domínio não é um Guid v7 preenchido na criação");

        var retried = await host.AdvanceUntilAsync(() => faults.Attempts >= 2, TimeSpan.FromMinutes(1), Step, ct);
        retried.ShouldNotBeNull("a outbox não tentou de novo depois da falha logo após a publicação");

        // Ciclos suficientes para a inbox do TemplateB processar tudo o que recebeu (cada passo espera os jobs).
        var inboxRunsBefore = host.CompletedExecutions("ProcessInbox");
        await host.AdvanceAsync(TimeSpan.FromSeconds(20), Step, ct);
        host.CompletedExecutions("ProcessInbox").ShouldBeGreaterThan(inboxRunsBefore, "o job da inbox não rodou depois da nova tentativa");

        faults.Attempts.ShouldBe(2, "esperadas 1 falha depois de publicar e 1 sucesso, sem novas execuções depois do sucesso");
        var received = probe.Received.Where(e => e.Name == token).ToList();
        received.Count.ShouldBe(1, "o retry da outbox gerou um segundo evento de integração e o TemplateB o processou de novo");

        received[0].Id.ShouldBe(domainEventId, "o evento de integração não reaproveitou o Id do evento de domínio");
    }
}
