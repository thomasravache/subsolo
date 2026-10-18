using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Profiles.Application.Features.Shared;
using Subsolo.Modules.Profiles.Domain.Profiles;

namespace Subsolo.Modules.Profiles.Application.Features.CreateOwnerlessProfile;

/// <summary>
/// SPEC-0021 — grava o perfil aprovado sem dono com o slug gerado do nome; perfil do mesmo tipo com o slug →
/// <see cref="ProfilesErrors.NameTaken"/>.
/// </summary>
internal sealed partial class CreateOwnerlessProfileCommandHandler(
    IProfileRepository profiles,
    IProfilesUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<CreateOwnerlessProfileCommandHandler> logger)
    : ICommandHandler<CreateOwnerlessProfileCommand, OwnerlessProfileCreated>
{
    public async Task<Result<OwnerlessProfileCreated>> Handle(CreateOwnerlessProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var draft = command.ToDraft();
        if (await profiles.ExistsAsync(draft.Type, draft.Slug, cancellationToken))
        {
            return Result.Failure<OwnerlessProfileCreated>(ProfilesErrors.NameTakenError());
        }

        var profile = Profile.Create(draft, ownerUserId: null, clock.GetUtcNow());
        profiles.Add(profile);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ProfilesConflictException conflict) when (conflict.Conflict == ProfilesConflict.NameTaken)
        {
            // Emenda 3: outro perfil com o mesmo tipo e nome foi gravado no meio (índice único).
            return Result.Failure<OwnerlessProfileCreated>(ProfilesErrors.NameTakenError());
        }
        LogCreated(logger, profile.Id);
        return Result.Success(new OwnerlessProfileCreated(profile.Id));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Perfil sem dono {ProfileId} criado pelo admin")]
    private static partial void LogCreated(ILogger logger, Guid profileId);
}
