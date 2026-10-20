namespace Subsolo.Modules.Profiles.IntegrationEvents;

/// <summary>
/// SPEC-0021 — leitura síncrona dos perfis por outros módulos (ADR-0001: interface pública em IntegrationEvents; o
/// cadastro de show usa para saber de quais perfis o usuário é dono). Nunca expõe o contato nem a prova dos pedidos.
/// </summary>
public interface IProfilesApi
{
    /// <summary>Os perfis aprovados de que <paramref name="userId"/> é dono (vazio se nenhum).</summary>
    Task<IReadOnlyList<ProfileSummary>> GetOwnedProfilesAsync(Guid userId, CancellationToken ct);

    /// <summary>O perfil <paramref name="profileId"/>, ou <see langword="null"/>.</summary>
    Task<ProfileSummary?> GetAsync(Guid profileId, CancellationToken ct);

    /// <summary>
    /// SPEC-0029 (H27-06) — os perfis entre <paramref name="profileIds"/>, por id, numa consulta só (os que não existem
    /// ficam de fora).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ProfileSummary>> GetManyAsync(IReadOnlyCollection<Guid> profileIds, CancellationToken ct);
}

/// <summary>
/// SPEC-0021 — um perfil para outros módulos: tipo (<c>band</c> | <c>venue</c> | <c>producer</c>), nome, slug, cidade
/// (slug) e o dono (<see langword="null"/> = sem dono).
/// </summary>
public sealed record ProfileSummary(Guid Id, string Type, string Name, string Slug, string City, Guid? OwnerUserId);
