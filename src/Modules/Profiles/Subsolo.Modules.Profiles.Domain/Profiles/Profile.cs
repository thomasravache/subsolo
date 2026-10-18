using Subsolo.Common.Domain;

namespace Subsolo.Modules.Profiles.Domain.Profiles;

/// <summary>
/// SPEC-0021 — perfil aprovado de banda, casa ou produtor: os dados do <see cref="ProfileDraft"/>, o dono (usuário ou
/// nenhum) e a data de criação. Ganhar dono levanta <see cref="ProfileOwnerAssignedDomainEvent"/>.
/// </summary>
public sealed class Profile : Entity
{
    /// <summary>Tamanho mínimo do nome.</summary>
    public const int NameMinLength = 2;

    /// <summary>Tamanho máximo do nome.</summary>
    public const int NameMaxLength = 80;

    private Profile()
    {
    }

    public Guid Id { get; private set; }

    /// <summary><see cref="ProfileTypes"/>.</summary>
    public string Type { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>Gerado do nome, único por tipo, imutável.</summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>Slug da cidade.</summary>
    public string City { get; private set; } = string.Empty;

    /// <summary>Slugs dos estilos (só banda).</summary>
    public IReadOnlyList<string> Styles { get; private set; } = [];

    public ProfileLinks Links { get; private set; } = ProfileLinks.None;

    /// <summary>Dono do perfil; <see langword="null"/> = sem dono (pode ser reivindicado).</summary>
    public Guid? OwnerUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// Cria o perfil aprovado a partir de <paramref name="draft"/>; com <paramref name="ownerUserId"/>, já com o dono (e
    /// o evento <see cref="ProfileOwnerAssignedDomainEvent"/>); sem, um perfil sem dono.
    /// </summary>
    public static Profile Create(ProfileDraft draft, Guid? ownerUserId, DateTimeOffset nowUtc) =>
        throw new NotImplementedException("SPEC-0021: Profile.Create ainda não implementado.");

    /// <summary>O perfil sem dono passa a ter <paramref name="userId"/> como dono (evento <see cref="ProfileOwnerAssignedDomainEvent"/>).</summary>
    public void AssignOwner(Guid userId) =>
        throw new NotImplementedException("SPEC-0021: Profile.AssignOwner ainda não implementado.");
}
