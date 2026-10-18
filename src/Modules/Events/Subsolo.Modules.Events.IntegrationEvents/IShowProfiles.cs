namespace Subsolo.Modules.Events.IntegrationEvents;

/// <summary>
/// SPEC-0023 (Emenda v2) — leitura dos perfis pelo cadastro de show, no contrato do próprio Events (ADR-0001: o
/// Profiles depende do Events, nunca o contrário). Implementada pelo <c>Profiles.Infrastructure</c>. Nunca expõe o
/// contato nem a prova dos pedidos.
/// </summary>
public interface IShowProfiles
{
    /// <summary>Os perfis aprovados de que <paramref name="userId"/> é dono (vazio se nenhum).</summary>
    Task<IReadOnlyList<ShowProfile>> GetOwnedProfilesAsync(Guid userId, CancellationToken ct);

    /// <summary>Os perfis aprovados entre <paramref name="profileIds"/>, por id (os inexistentes ficam de fora).</summary>
    Task<IReadOnlyDictionary<Guid, ShowProfile>> GetManyAsync(IReadOnlyCollection<Guid> profileIds, CancellationToken ct);
}

/// <summary>
/// SPEC-0023 — um perfil visto pelo Events: tipo (<c>band</c> | <c>venue</c> | <c>producer</c>), nome, slug, cidade
/// (slug) e o dono (<see langword="null"/> = sem dono).
/// </summary>
public sealed record ShowProfile(Guid Id, string Type, string Name, string Slug, string City, Guid? OwnerUserId);
