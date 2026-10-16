using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Application.Decorators;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.UnitTests.Infrastructure;
using Subsolo.Modules.Template.Application.Features.GetTemplateItemName;
using Subsolo.Modules.Template.Application.Features.RenameTemplateItem;

namespace Subsolo.Common.UnitTests;

/// <summary>
/// SPEC-0007 UT-06 — comandos e consultas executados pelos decorators (módulo registrado com <c>AddModule</c>) geram
/// uma atividade numa <see cref="ActivitySource"/> do Subsolo (nome começando por <c>Subsolo</c>), com o nome da
/// mensagem e o resultado: status <see cref="ActivityStatusCode.Ok"/> no sucesso; <see cref="ActivityStatusCode.Error"/>
/// e o código do erro numa tag na falha. Cada teste filtra as atividades pelo próprio trace.
/// </summary>
public sealed class TracingTests : IDisposable
{
    private readonly ConcurrentQueue<Activity> _stopped = new();
    private readonly ActivityListener _listener;

    public TracingTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith("Subsolo", StringComparison.Ordinal),
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _stopped.Enqueue,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    [Trait("Spec", "SPEC-0007:UT-06")]
    public async Task SuccessfulCommandIsTracedWithItsNameAndOkStatus()
    {
        await using var services = TemplateModuleServices.Build();
        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<RenameTemplateItemCommand>>();

        var activity = await TraceAsync(
            () => handler.Handle(new RenameTemplateItemCommand(Guid.NewGuid(), "Novo nome"), TestContext.Current.CancellationToken),
            nameof(RenameTemplateItemCommand));

        activity.Status.ShouldBe(ActivityStatusCode.Ok, "a atividade não registra o resultado de sucesso");
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:UT-06")]
    public async Task FailedCommandIsTracedWithErrorStatusAndErrorCode()
    {
        await using var services = TemplateModuleServices.Build();
        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<RenameTemplateItemCommand>>();

        var activity = await TraceAsync(
            () => handler.Handle(new RenameTemplateItemCommand(Guid.Empty, string.Empty), TestContext.Current.CancellationToken),
            nameof(RenameTemplateItemCommand));

        activity.Status.ShouldBe(ActivityStatusCode.Error, "a atividade não registra a falha");
        activity.TagObjects.Select(tag => tag.Value?.ToString())
            .ShouldContain(ValidationDecorator.ErrorCode, "a atividade não registra o código do erro numa tag");
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:UT-06")]
    public async Task QueryIsTracedWithItsNameAndOkStatus()
    {
        await using var services = TemplateModuleServices.Build();
        await using var scope = services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IQueryHandler<GetTemplateItemNameQuery, string>>();

        var activity = await TraceAsync(
            () => handler.Handle(new GetTemplateItemNameQuery(Guid.NewGuid()), TestContext.Current.CancellationToken),
            nameof(GetTemplateItemNameQuery));

        activity.Status.ShouldBe(ActivityStatusCode.Ok, "a atividade não registra o resultado de sucesso");
    }

    /// <summary>
    /// Executa <paramref name="execute"/> dentro de uma atividade-raiz própria e devolve a única atividade do Subsolo,
    /// no mesmo trace, cujo nome contém <paramref name="messageName"/>.
    /// </summary>
    private async Task<Activity> TraceAsync<TResult>(Func<Task<TResult>> execute, string messageName)
    {
        ActivityTraceId traceId;
        using (var root = new Activity($"SPEC-0007:UT-06:{messageName}").Start())
        {
            traceId = root.TraceId;
            await execute();
        }

        var traced = _stopped
            .Where(activity => activity.TraceId == traceId)
            .Where(activity => activity.DisplayName.Contains(messageName, StringComparison.Ordinal) ||
                               activity.OperationName.Contains(messageName, StringComparison.Ordinal))
            .ToList();
        traced.ShouldNotBeEmpty($"nenhuma atividade de uma ActivitySource \"Subsolo*\" com o nome {messageName}");
        return traced.ShouldHaveSingleItem();
    }
}
