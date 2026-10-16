using Subsolo.Common.Application.EventBus;

// Viola: CycleA e CycleB dependem um do outro (cada um só pelo IntegrationEvents do outro, mas formam um ciclo).
namespace Subsolo.Modules.CycleA.IntegrationEvents
{
    public sealed record AHappened(Guid Id, DateTime OccurredOnUtc) : IIntegrationEvent;
}

namespace Subsolo.Modules.CycleA.Application
{
    public sealed class OnBHappened : IIntegrationEventHandler<CycleB.IntegrationEvents.BHappened>
    {
        public Task Handle(CycleB.IntegrationEvents.BHappened integrationEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}

namespace Subsolo.Modules.CycleB.IntegrationEvents
{
    public sealed record BHappened(Guid Id, DateTime OccurredOnUtc) : IIntegrationEvent;
}

namespace Subsolo.Modules.CycleB.Application
{
    public sealed class OnAHappened : IIntegrationEventHandler<CycleA.IntegrationEvents.AHappened>
    {
        public Task Handle(CycleA.IntegrationEvents.AHappened integrationEvent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
