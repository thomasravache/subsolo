using Subsolo.Modules.Events.IntegrationEvents;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.NotifyNewShow;

/// <summary>
/// SPEC-0031 — o compositor do aviso <c>new_show</c> (§6): título "Show novo em {CityName}", corpo
/// "{Lineup separado por ' + '} · {ddd dd/MM HH'h'mm no fuso da cidade} · {VenueName}", url "/{CitySlug}/{Slug}" e chave
/// "new_show:{ShowId}".
/// </summary>
internal static class NewShowNotice
{
    // TODO(SPEC-0031): esqueleto do G1 (Test-writer); o Implementer escreve a regra.
    public static Notice Compose(ShowSummary show) => throw new NotImplementedException();
}
