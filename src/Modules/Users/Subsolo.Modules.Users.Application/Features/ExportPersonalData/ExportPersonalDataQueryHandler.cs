using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Application.Diagnostics;
using Subsolo.Modules.Users.Domain.Sessions;
using Subsolo.Modules.Users.Domain.Users;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Users.Application.Features.ExportPersonalData;

/// <summary>
/// SPEC-0019 — monta o export com a conta, as sessões do usuário e as seções registradas
/// (<see cref="IPersonalDataSection"/>); usuário apagado → <see cref="UsersErrors.UserNotFound"/>. Conta em
/// <c>subsolo.users.exports</c>.
/// </summary>
internal sealed class ExportPersonalDataQueryHandler(
    IUserRepository users,
    ISessionRepository sessions,
    IEnumerable<IPersonalDataSection> sections,
    TimeProvider clock)
    : IQueryHandler<ExportPersonalDataQuery, PersonalDataExport>
{
    public async Task<Result<PersonalDataExport>> Handle(ExportPersonalDataQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var user = await users.GetByIdAsync(query.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<PersonalDataExport>(UsersErrors.UserNotFoundError());
        }

        var userSessions = await sessions.ListByUserAsync(user.Id, cancellationToken);
        var exportedSections = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var section in sections)
        {
            if (await section.ExportAsync(user.Id, cancellationToken) is { } content)
            {
                exportedSections[section.Name] = content;
            }
        }

        UsersTelemetry.PersonalDataExported();
        return Result.Success(new PersonalDataExport(
            clock.GetUtcNow(),
            new PersonalDataAccount(user.Id, user.Email, user.DisplayName, [.. user.Roles], user.CreatedAtUtc),
            [.. userSessions.Select(session => new PersonalDataSession(
                session.CreatedAtUtc,
                SignInMethods.ToCode(session.Method),
                session.LastSeenAtUtc,
                session.ExpiresAtUtc,
                session.AbsoluteExpiresAtUtc,
                session.RevokedAtUtc))],
            exportedSections));
    }
}
