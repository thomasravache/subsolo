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
}
