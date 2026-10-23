using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Modules.Notifications.Application.Features.Unsubscribe;

/// <summary>SPEC-0030 — <c>POST /api/v1/notifications/unsubscribe</c>: desliga o e-mail do tipo do token, sem login.</summary>
internal sealed record UnsubscribeEmailCommand(string? Token) : ICommand;

/// <summary>
/// SPEC-0030 — token íntegro → desliga o e-mail daquele tipo (idempotente); senão, 400
/// <see cref="UnsubscribeErrors.InvalidToken"/> sem mudar nada.
/// </summary>
internal sealed class UnsubscribeEmailCommandHandler(IUnsubscribeTokens tokens, INotificationsStore store)
    : ICommandHandler<UnsubscribeEmailCommand>
{
    public async Task<Result> Handle(UnsubscribeEmailCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (tokens.Read(command.Token) is not { } target)
        {
            return Result.Failure(UnsubscribeErrors.Invalid);
        }

        await store.TurnOffEmailAsync(target.UserId, target.Kind, cancellationToken);
        return Result.Success();
    }
}

/// <summary>SPEC-0030 — erro do descadastro (interno ao módulo: o contrato público de e-mail da SPEC-0018 não muda).</summary>
internal static class UnsubscribeErrors
{
    public const string InvalidToken = "notifications.invalid_token";

    public static readonly Error Invalid = Error.Validation(
        InvalidToken,
        "O link de descadastro é inválido.",
        new Dictionary<string, string[]>(StringComparer.Ordinal) { ["token"] = ["Token ausente, adulterado ou de outro tipo."] });
}
