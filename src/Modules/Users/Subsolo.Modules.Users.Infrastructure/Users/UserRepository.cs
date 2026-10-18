using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Users.Domain.Users;

namespace Subsolo.Modules.Users.Infrastructure.Users;

/// <summary>SPEC-0014 — contas no schema <c>users</c> (rastreadas: a entrada pode mudar os papéis).</summary>
internal sealed class UserRepository(UsersDbContext db) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Users.FindAsync([id], cancellationToken);

    public void Add(User user) => db.Users.Add(user);

    public void Remove(User user) => db.Users.Remove(user);
}
