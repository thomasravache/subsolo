namespace Subsolo.Modules.Engagement.IntegrationEvents;

/// <summary>
/// SPEC-0027 — leitura síncrona dos follows por outros módulos (ADR-0001: interface pública em IntegrationEvents): os
/// avisos de show novo perguntam quem segue a cidade do show ou algum dos perfis dele.
/// </summary>
public interface IEngagementApi
{
    /// <summary>
    /// Os ids, sem repetição, dos usuários que seguem a cidade <paramref name="citySlug"/> ou algum dos perfis
    /// <paramref name="profileIds"/>.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> GetFollowersAsync(string citySlug, IReadOnlyCollection<Guid> profileIds, CancellationToken ct);
}
