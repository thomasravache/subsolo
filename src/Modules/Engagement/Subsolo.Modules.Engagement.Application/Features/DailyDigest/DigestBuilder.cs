using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.DailyDigest;

/// <summary>
/// SPEC-0031 — o montador do resumo diário (§6): dos shows publicados nas últimas 24 h ainda <c>scheduled</c> e futuros,
/// um aviso <c>digest</c> por seguidor com os shows dele, sem repetição (até <see cref="MaxShows"/>, pela data, e
/// "e mais {n} na agenda"); quem não tem show novo fica de fora.
/// </summary>
// TODO(SPEC-0031): esqueleto do G1 (Test-writer). O Implementer recebe as dependências no construtor (o harness dos
// testes unitários registra IEventsCatalog, IEngagementApi, IFollowsReadModel, IPublishedShows, TimeProvider e ILogger<T>).
internal sealed class DigestBuilder
{
    /// <summary>Máximo de shows listados no corpo do resumo.</summary>
    public const int MaxShows = 20;

#pragma warning disable CA1822 // esqueleto: a implementação usa as dependências da instância
    public Task<IReadOnlyList<DigestNotice>> BuildAsync(
        IReadOnlyCollection<PublishedShow> published, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
#pragma warning restore CA1822
}

/// <summary>SPEC-0031 — o aviso <c>digest</c> de um fã.</summary>
internal sealed record DigestNotice(Guid UserId, Notice Notice);
