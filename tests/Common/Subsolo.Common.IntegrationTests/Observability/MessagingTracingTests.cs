using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Infrastructure.Messaging;
using Subsolo.Common.IntegrationTests.Infrastructure;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Template.Application;
using Subsolo.Modules.Template.Domain;
using Subsolo.Modules.Template.Infrastructure;
using Subsolo.Modules.TemplateB.Application;

namespace Subsolo.Common.IntegrationTests.Observability;

/// <summary>
/// SPEC-0009 (item herdado da revisão da base do monólito) — cada mensagem processada pelos jobs da outbox e da inbox
/// gera uma atividade da fonte <see cref="MessagingTelemetry.SourceName"/>: <c>ProcessOutbox &lt;Evento&gt;</c> no
/// módulo que publicou e <c>ProcessInbox &lt;Evento&gt;</c> no módulo que consome, com as tags do módulo e do Id da
/// mensagem; status Ok no sucesso e Error, com a exceção registrada, numa falha do handler.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class MessagingTracingTests : IDisposable
{
    private readonly PostgresFixture _postgres;
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(1);

    private readonly ConcurrentQueue<Activity> _stopped = new();
    private readonly ActivityListener? _listener;

    public MessagingTracingTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _listener = Listen(_stopped);
    }

    public void Dispose() => _listener?.Dispose();

    [Fact]
    [Trait("Spec", "SPEC-0009:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task OutboxAndInboxProcessingAreTracedWithModuleAndMessageId()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ModulesHost.CreateAsync(_postgres, ct);
        await host.MigrateAllAsync(ct);
        await host.StartAsync(ct);
        var probe = host.Services.GetRequiredService<TemplateBProbe>();
        var token = $"item-{Guid.NewGuid():N}";
        var domainEventId = await CreateItemAsync(host, token, ct);

        var elapsed = await host.AdvanceUntilAsync(() => probe.Received.Any(e => e.Name == token), TimeSpan.FromSeconds(10), Step, ct);
        elapsed.ShouldNotBeNull("o evento não atravessou outbox → inbox");
        var integrationEventId = probe.Received.Single(e => e.Name == token).Id;

        var outbox = ForMessage(domainEventId, "ProcessOutbox ").ShouldHaveSingleItem("a mensagem da outbox não gerou exatamente uma atividade Subsolo.Messaging");
        outbox.Status.ShouldBe(ActivityStatusCode.Ok);
        Tag(outbox, MessagingTelemetry.ModuleTag).ShouldStartWith("Template");
        Tag(outbox, MessagingTelemetry.ModuleTag).ShouldNotStartWith("TemplateB");

        var inbox = ForMessage(integrationEventId, "ProcessInbox ").ShouldHaveSingleItem("a mensagem da inbox não gerou exatamente uma atividade Subsolo.Messaging");
        inbox.Status.ShouldBe(ActivityStatusCode.Ok);
        Tag(inbox, MessagingTelemetry.ModuleTag).ShouldStartWith("TemplateB");
    }

    [Fact]
    [Trait("Spec", "SPEC-0009:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task FailedOutboxHandlerIsTracedAsErrorWithTheException()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ModulesHost.CreateAsync(_postgres, ct);
        await host.MigrateAllAsync(ct);
        host.Services.GetRequiredService<TemplateFaults>().FailNext(1);
        await host.StartAsync(ct);
        var probe = host.Services.GetRequiredService<TemplateBProbe>();
        var token = $"item-{Guid.NewGuid():N}";
        var domainEventId = await CreateItemAsync(host, token, ct);

        var elapsed = await host.AdvanceUntilAsync(
            () => probe.Received.Any(e => e.Name == token), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5), ct);
        elapsed.ShouldNotBeNull("a outbox não tentou de novo depois da falha");

        var attempts = ForMessage(domainEventId, "ProcessOutbox ");
        attempts.Count.ShouldBeGreaterThanOrEqualTo(2, "esperadas a tentativa com falha e a de sucesso");
        var failed = attempts.First();
        failed.Status.ShouldBe(ActivityStatusCode.Error, "a falha do handler não marcou a atividade com erro");
        failed.Events.ShouldContain(e => e.Name == "exception", "a exceção do handler não foi registrada na atividade");
        attempts.Last().Status.ShouldBe(ActivityStatusCode.Ok);
    }

    private static async Task<Guid> CreateItemAsync(ModulesHost host, string token, CancellationToken ct)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TemplateDbContext>();
        var item = TemplateItem.Create(token);
        var eventId = item.DomainEvents.ShouldHaveSingleItem().Id;
        context.Items.Add(item);
        await context.SaveChangesAsync(ct);
        return eventId;
    }

    /// <summary>Atividades da mensagem <paramref name="messageId"/> cujo nome começa por <paramref name="operation"/>.</summary>
    private List<Activity> ForMessage(Guid messageId, string operation) =>
        [.. _stopped.Where(activity => Tag(activity, MessagingTelemetry.MessageIdTag) == messageId.ToString())
            .Where(activity => activity.DisplayName.StartsWith(operation, StringComparison.Ordinal))
            .OrderBy(activity => activity.StartTimeUtc)];

    private static string? Tag(Activity activity, string name) => activity.GetTagItem(name)?.ToString();

    private static ActivityListener Listen(ConcurrentQueue<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MessagingTelemetry.SourceName,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
