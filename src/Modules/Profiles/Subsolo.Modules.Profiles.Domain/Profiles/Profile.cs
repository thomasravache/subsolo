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
    public static Profile Create(ProfileDraft draft, Guid? ownerUserId, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Slug);
        if (!ProfileTypes.All.Contains(draft.Type, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Tipo de perfil desconhecido: {draft.Type}.", nameof(draft));
        }

        var profile = new Profile
        {
            Id = Guid.CreateVersion7(),
            Type = draft.Type,
            Name = draft.Name,
            Slug = draft.Slug,
            City = draft.City,
            Styles = [.. draft.Styles],
            Links = draft.Links,
            CreatedAtUtc = nowUtc,
        };
        if (ownerUserId is { } owner)
        {
            profile.AssignOwner(owner);
        }

        return profile;
    }

    /// <summary>O perfil sem dono passa a ter <paramref name="userId"/> como dono (evento <see cref="ProfileOwnerAssignedDomainEvent"/>).</summary>
    public void AssignOwner(Guid userId)
    {
        if (OwnerUserId is not null)
        {
            throw new InvalidOperationException("O perfil já tem dono.");
        }

        OwnerUserId = userId;
        Raise(new ProfileOwnerAssignedDomainEvent(Id, userId));
    }

    /// <summary>
    /// O dono excluiu a conta: o perfil fica sem dono (o histórico de shows é da cena) e pode ser reivindicado de novo.
    /// </summary>
    public void ReleaseOwner() => OwnerUserId = null;
}
