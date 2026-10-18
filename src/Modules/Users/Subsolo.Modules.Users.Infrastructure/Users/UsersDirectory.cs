using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Users.Infrastructure.Users;

/// <summary>SPEC-0021 (Emenda v2) — <see cref="IUsersDirectory"/> sobre <c>users.users</c>, numa só consulta.</summary>
internal sealed class UsersDirectory(UsersDbContext db) : IUsersDirectory
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetEmailsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var ids = userIds.Distinct().ToList();
        return await db.Users.AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email, ct);
    }
}
