using Subsolo.Modules.Profiles.Application.Features.GetPublicProfile;
using Subsolo.Modules.Profiles.Application.Features.ListMyProfiles;
using Subsolo.Modules.Profiles.Application.Features.ListProfileRequests;
using Subsolo.Modules.Profiles.Application.Features.LookupProfiles;
using Subsolo.Modules.Profiles.Application.Features.SearchClaimableProfiles;

namespace Subsolo.Modules.Profiles.Application;

/// <summary>SPEC-0021 — leituras do schema <c>profiles</c> para as consultas (sem rastreamento, sem passar pelo domínio).</summary>
internal interface IProfilesReadModel
{
    /// <summary>
    /// Até <paramref name="limit"/> perfis aprovados sem dono cujo nome contém <paramref name="term"/> (sem diferenciar
    /// caixa) ou cujo slug contém <paramref name="termSlug"/>, do <paramref name="type"/> quando informado, por nome.
    /// </summary>
    Task<IReadOnlyList<ClaimableProfileResponse>> SearchClaimableAsync(
        string term, string termSlug, string? type, int limit, CancellationToken cancellationToken);

    /// <summary>SPEC-0026 — o perfil aprovado do <paramref name="type"/> com o <paramref name="slug"/>, ou <see langword="null"/>.</summary>
    Task<PublicProfileData?> GetPublicAsync(string type, string slug, CancellationToken cancellationToken);

    /// <summary>
    /// SPEC-0026 (H23-08) — até <paramref name="limit"/> perfis aprovados, com ou sem dono, cujo nome contém
    /// <paramref name="term"/> (sem diferenciar caixa) ou cujo slug contém <paramref name="termSlug"/>, do
    /// <paramref name="type"/> quando informado, por nome.
    /// </summary>
    Task<IReadOnlyList<ProfileLookupData>> LookupAsync(
        string term, string termSlug, string? type, int limit, CancellationToken cancellationToken);

    /// <summary>Os perfis de que <paramref name="userId"/> é dono e os pedidos dele (mais novos primeiro), sem contato nem prova.</summary>
    Task<MyProfilesResponse> ListMineAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Os pedidos pendentes, mais antigos primeiro, com o contato ou a prova (só para a fila do admin).</summary>
    Task<IReadOnlyList<PendingProfileRequest>> ListPendingAsync(CancellationToken cancellationToken);
}

/// <summary>
/// SPEC-0021 — gravações em lote do módulo (fora do domínio): a exclusão da conta de um usuário e a limpeza periódica
/// dos pedidos decididos (ADR-0008).
/// </summary>
internal interface IProfilesMaintenance
{
    /// <summary>Os perfis de <paramref name="userId"/> ficam sem dono; devolve quantos.</summary>
    Task<int> ReleaseOwnedProfilesAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Apaga os pedidos (e o contato ou a prova) de <paramref name="userId"/>; devolve quantos.</summary>
    Task<int> DeleteRequestsOfAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Apaga os pedidos decididos antes de <paramref name="cutoffUtc"/>; devolve quantos.</summary>
    Task<int> DeleteDecidedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken);
}
