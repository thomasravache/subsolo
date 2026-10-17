using Subsolo.Modules.Users.Domain.Sessions;

namespace Subsolo.Modules.Users.Infrastructure.Sessions;

/// <summary>SPEC-0014 — sessões no schema <c>users</c>, buscadas pela chave primária (hash do token).</summary>
internal sealed class SessionRepository(UsersDbContext db) : ISessionRepository
{
    public async Task<Session?> GetByIdHashAsync(string idHash, CancellationToken cancellationToken) =>
        await db.Sessions.FindAsync([idHash], cancellationToken);

    public void Add(Session session) => db.Sessions.Add(session);
}
