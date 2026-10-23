using Microsoft.EntityFrameworkCore;
using Subsolo.Modules.Notifications.IntegrationEvents;

namespace Subsolo.Modules.Users.Infrastructure.Users;

/// <summary>
/// SPEC-0030 — <see cref="IRecipientDirectory"/> (contrato do Notifications, ADR-0001/ADR-0014) sobre <c>users.users</c>:
/// o e-mail dos destinatários dos avisos, numa só consulta; os usuários que não existem (excluídos) ficam de fora.
/// </summary>
internal sealed class RecipientDirectory(UsersDbContext db) : IRecipientDirectory
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
