namespace Subsolo.Modules.Users.Domain.Sessions;

/// <summary>SPEC-0014 — sessões no schema <c>users</c>, buscadas pelo hash do id.</summary>
public interface ISessionRepository
{
    /// <summary>A sessão com o hash de id <paramref name="idHash"/>, ou <see langword="null"/>.</summary>
    Task<Session?> GetByIdHashAsync(string idHash, CancellationToken cancellationToken);

    /// <summary>Inclui uma sessão nova (gravada no próximo <c>SaveChangesAsync</c>).</summary>
    void Add(Session session);
}
