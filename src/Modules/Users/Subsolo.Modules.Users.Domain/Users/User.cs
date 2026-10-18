using Subsolo.Common.Domain;

namespace Subsolo.Modules.Users.Domain.Users;

/// <summary>
/// SPEC-0014 — conta do usuário: e-mail normalizado (minúsculas, sem espaços) e único, nome de exibição opcional (até
/// <see cref="DisplayNameMaxLength"/> caracteres), papéis (<see cref="UserRoles.Fan"/> sempre; <see cref="UserRoles.Admin"/>
/// quando o e-mail está em <c>Users:AdminEmails</c>) e data de criação. Na primeira entrada levanta
/// <see cref="UserRegisteredDomainEvent"/>.
/// </summary>
public sealed class User : Entity
{
    /// <summary>Tamanho máximo do e-mail.</summary>
    public const int EmailMaxLength = 254;

    /// <summary>Tamanho máximo do nome de exibição.</summary>
    public const int DisplayNameMaxLength = 60;

    private User()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>E-mail normalizado: minúsculas, sem espaços.</summary>
    public string Email { get; private set; } = string.Empty;

    public string? DisplayName { get; private set; }

    /// <summary>Papéis atuais (<see cref="UserRoles"/>).</summary>
    public IReadOnlyList<string> Roles { get; private set; } = [];

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// Cria a conta na primeira entrada com o e-mail já normalizado: papel <see cref="UserRoles.Fan"/> (e
    /// <see cref="UserRoles.Admin"/> quando <paramref name="isAdmin"/>) e o evento <see cref="UserRegisteredDomainEvent"/>.
    /// </summary>
    public static User Register(string normalizedEmail, bool isAdmin, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(normalizedEmail.Length, EmailMaxLength, nameof(normalizedEmail));
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = normalizedEmail,
            CreatedAtUtc = nowUtc,
            Roles = RolesFor(isAdmin, []),
        };
        user.Raise(new UserRegisteredDomainEvent(user.Id));
        return user;
    }

    /// <summary>
    /// SPEC-0019 — troca o nome de exibição pelo valor já validado (sem espaços nas pontas, 1 a
    /// <see cref="DisplayNameMaxLength"/> caracteres) ou o remove (<see langword="null"/>).
    /// </summary>
    public void ChangeDisplayName(string? displayName)
    {
        if (displayName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(displayName.Length, DisplayNameMaxLength, nameof(displayName));
        }

        DisplayName = displayName;
    }

    /// <summary>SPEC-0019 — a conta vai ser apagada: levanta <see cref="UserDeletedDomainEvent"/> (só o id).</summary>
    public void Delete() => Raise(new UserDeletedDomainEvent(Id));

    /// <summary>Dá ou tira o papel <see cref="UserRoles.Admin"/> conforme a configuração atual (os outros papéis ficam).</summary>
    public void SyncAdminRole(bool isAdmin)
    {
        if (Roles.Contains(UserRoles.Admin, StringComparer.Ordinal) != isAdmin)
        {
            Roles = RolesFor(isAdmin, Roles);
        }
    }

    private static string[] RolesFor(bool isAdmin, IEnumerable<string> current)
    {
        var others = current.Where(role => role is not UserRoles.Fan and not UserRoles.Admin);
        return isAdmin ? [UserRoles.Fan, .. others, UserRoles.Admin] : [UserRoles.Fan, .. others];
    }
}
