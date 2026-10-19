using Subsolo.Modules.Engagement.Domain.Follows;
using Subsolo.Modules.Engagement.IntegrationEvents;

namespace Subsolo.Modules.Engagement.Application.Features.GetFollowers;

#pragma warning disable CS9113 // Scaffold do contrato (SPEC-0027): o repositório é usado pela implementação.

/// <summary>
/// SPEC-0027 — <see cref="IEngagementApi"/>: quem segue a cidade ou algum dos perfis, pelos follows de
/// <see cref="IFollowRepository.GetFollowerIdsAsync"/>, sem repetição.
/// </summary>
internal sealed class EngagementApi(IFollowRepository follows) : IEngagementApi
{
    public Task<IReadOnlyCollection<Guid>> GetFollowersAsync(
        string citySlug, IReadOnlyCollection<Guid> profileIds, CancellationToken ct) =>
        throw new NotImplementedException("SPEC-0027: IEngagementApi ainda não implementado.");
}

#pragma warning restore CS9113
