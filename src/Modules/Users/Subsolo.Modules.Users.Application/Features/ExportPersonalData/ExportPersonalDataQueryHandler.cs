using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Domain.Sessions;
using Subsolo.Modules.Users.Domain.Users;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Users.Application.Features.ExportPersonalData;

/// <summary>
/// SPEC-0019 — monta o export com a conta, as sessões do usuário e as seções registradas
/// (<see cref="IPersonalDataSection"/>); usuário apagado → <see cref="UsersErrors.UserNotFound"/>.
/// </summary>
internal sealed class ExportPersonalDataQueryHandler(
    IUserRepository users,
    ISessionRepository sessions,
    IEnumerable<IPersonalDataSection> sections,
    TimeProvider clock)
    : IQueryHandler<ExportPersonalDataQuery, PersonalDataExport>
{
    public Task<Result<PersonalDataExport>> Handle(ExportPersonalDataQuery query, CancellationToken cancellationToken) =>
        throw new NotImplementedException(
            $"SPEC-0019: scaffold — implementar no Green ({users}, {sessions}, {sections}, {clock}).");
}
