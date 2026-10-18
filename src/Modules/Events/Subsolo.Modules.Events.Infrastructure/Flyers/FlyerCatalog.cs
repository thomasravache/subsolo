using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Domain.Flyers;

namespace Subsolo.Modules.Events.Infrastructure.Flyers;

/// <summary>
/// SPEC-0022 — <see cref="IFlyerCatalog"/> para o cadastro de show: confere que o flyer existe e o liga ao show (a
/// ligação é gravada com o <see cref="IEventsUnitOfWork"/>, na mesma transação do show). Ligar um id inexistente não faz
/// nada (quem liga confere antes com <see cref="ExistsAsync"/>).
/// </summary>
internal sealed class FlyerCatalog(IFlyerRepository flyers, TimeProvider clock) : IFlyerCatalog
{
    public Task<bool> ExistsAsync(string flyerId, CancellationToken ct) => flyers.ExistsAsync(flyerId, ct);

    public async Task AttachAsync(string flyerId, CancellationToken ct)
    {
        var flyer = await flyers.GetAsync(flyerId, ct);
        flyer?.Attach(clock.GetUtcNow());
    }
}
