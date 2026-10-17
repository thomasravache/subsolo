namespace Subsolo.Modules.Users.Domain.Sessions;

/// <summary>SPEC-0014 — sessões no schema <c>users</c>, buscadas pelo hash SHA-256 do token da sessão.</summary>
public interface ISessionRepository
{
    /// <summary>A sessão cujo token tem o hash <paramref name="idHash"/>, ou <see langword="null"/>.</summary>
    Task<Session?> GetByIdHashAsync(string idHash, CancellationToken cancellationToken);

    /// <summary>Inclui uma sessão nova (gravada no próximo <c>SaveChangesAsync</c>).</summary>
    void Add(Session session);
}
