using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Template.Domain;
using Subsolo.Modules.Template.Infrastructure;
using Subsolo.Modules.TemplateB.Application;

namespace Subsolo.Common.IntegrationTests;

/// <summary>
/// SPEC-0007 IT-03 — o evento publicado pelo módulo Template chega ao handler do TemplateB pelos jobs Quartz
/// (<c>ProcessOutbox</c> do Template → <c>IEventBus</c> → <c>ProcessInbox</c> do TemplateB) em até 5 s de tempo
/// simulado, com o <see cref="TimeProvider"/> da aplicação trocado por um <c>FakeTimeProvider</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class CrossModuleDeliveryTests(PostgresFixture postgres)
{
    private static readonly TimeSpan DeliveryLimit = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(1);

    [Fact]
    [Trait("Spec", "SPEC-0007:IT-03")]
    [Trait("Kind", "Integration")]
    public async Task EventPublishedByModuleAReachesModuleBHandlerWithinFiveSimulatedSeconds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ModulesHost.CreateAsync(postgres, ct);
        await host.MigrateAllAsync(ct);
        await host.StartAsync(ct);
        var probe = host.Services.GetRequiredService<TemplateBProbe>();
        var token = $"item-{Guid.NewGuid():N}";

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
            context.Items.Add(TemplateItem.Create(token));
            await context.SaveChangesAsync(ct);
        }

        var elapsed = await host.AdvanceUntilAsync(() => probe.Received.Any(e => e.Name == token), DeliveryLimit, Step, ct);

        elapsed.ShouldNotBeNull($"o handler do TemplateB não recebeu o evento em {DeliveryLimit.TotalSeconds} s simulados");
        elapsed.Value.ShouldBeLessThanOrEqualTo(DeliveryLimit);
        probe.Received.Count(e => e.Name == token).ShouldBe(1);
    }
}
