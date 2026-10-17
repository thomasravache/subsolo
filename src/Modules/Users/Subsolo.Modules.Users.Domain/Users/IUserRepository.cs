namespace Subsolo.Modules.Users.Domain.Users;

/// <summary>SPEC-0014 — contas de usuário no schema <c>users</c>.</summary>
public interface IUserRepository
{
    /// <summary>O usuário com o e-mail já normalizado, ou <see langword="null"/>.</summary>
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>O usuário com o <paramref name="id"/>, ou <see langword="null"/>.</summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Inclui um usuário novo (gravado no próximo <c>SaveChangesAsync</c>).</summary>
    void Add(User user);
}
