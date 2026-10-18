using System.Globalization;
using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Application.Features.GetMe;
using Subsolo.Modules.Users.Domain.Users;

namespace Subsolo.Modules.Users.Application.Features.UpdateDisplayName;

/// <summary>
/// SPEC-0019 — valida e grava o nome de exibição; fora da regra → <see cref="UsersErrors.InvalidDisplayName"/>, nada
/// gravado. O log leva só o id do usuário.
/// </summary>
internal sealed partial class UpdateDisplayNameCommandHandler(
    IUserRepository users,
    IUsersUnitOfWork unitOfWork,
    ILogger<UpdateDisplayNameCommandHandler> logger)
    : ICommandHandler<UpdateDisplayNameCommand, MeResponse>
{
    public async Task<Result<MeResponse>> Handle(UpdateDisplayNameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!TryNormalize(command.DisplayName, out var displayName))
        {
            return Result.Failure<MeResponse>(UsersErrors.InvalidDisplayNameError());
        }

        var user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<MeResponse>(UsersErrors.UserNotFoundError());
        }

        user.ChangeDisplayName(displayName);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogRenamed(logger, user.Id);
        return Result.Success(new MeResponse(user.Id, user.Email, user.DisplayName, [.. user.Roles]));
    }

    /// <summary>
    /// Sem espaços nas pontas; vazio ou <see langword="null"/> → sem nome. Inválido: mais de
    /// <see cref="User.DisplayNameMaxLength"/> caracteres ou só caracteres que não aparecem (espaços, controle, formato).
    /// </summary>
    internal static bool TryNormalize(string? value, out string? displayName)
    {
        displayName = value?.Trim();
        if (string.IsNullOrEmpty(displayName))
        {
            displayName = null;
            return true;
        }

        return displayName.Length <= User.DisplayNameMaxLength && displayName.Any(IsVisible);
    }

    private static bool IsVisible(char character) =>
        !char.IsWhiteSpace(character)
        && CharUnicodeInfo.GetUnicodeCategory(character) is not (UnicodeCategory.Control or UnicodeCategory.Format);

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuário {UserId} mudou o nome de exibição")]
    private static partial void LogRenamed(ILogger logger, Guid userId);
}
