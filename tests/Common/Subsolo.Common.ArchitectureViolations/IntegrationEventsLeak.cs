using Subsolo.Common.Application.EventBus;

// Viola: o IntegrationEvents depende do Domain do próprio módulo (só Common.Application é permitido).
namespace Subsolo.Modules.ContractLeak.IntegrationEvents
{
    public sealed record ShowPublished(Guid Id, DateTime OccurredOnUtc, Domain.Show Show) : IIntegrationEvent;
}

namespace Subsolo.Modules.ContractLeak.Domain
{
    public sealed class Show;
}
