using FluentValidation;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;

namespace Subsolo.Modules.Notifications.Application.Features.Subscriptions;

/// <summary>SPEC-0030 — <c>POST /api/v1/me/push-subscriptions</c>: inscreve o aparelho do fã (endpoint e chaves do navegador).</summary>
internal sealed record SubscribePushCommand(Guid UserId, string? Endpoint, string? P256dh, string? Auth) : ICommand;

/// <summary>
/// SPEC-0030 — a inscrição passa no <see cref="PushSubscriptionValidator"/> (serviço de push conhecido, https, chaves
/// presentes); senão <c>notifications.invalid_subscription</c> no campo <c>endpoint</c>.
/// </summary>
internal sealed class SubscribePushCommandValidator : AbstractValidator<SubscribePushCommand>
{
    public SubscribePushCommandValidator() =>
        RuleFor(command => command.Endpoint)
            .Must((command, _) => PushSubscriptionValidator.Validate(command.Endpoint, command.P256dh, command.Auth).IsSuccess)
            .OverridePropertyName("endpoint")
            .WithErrorCode(PushSubscriptionValidator.InvalidSubscription)
            .WithMessage("Use o endereço https de um serviço de push conhecido, com as chaves p256dh e auth.");
}

/// <summary>
/// SPEC-0030 — grava a inscrição (idempotente pelo endpoint); a primeira do usuário liga o show novo por push (§2).
/// </summary>
internal sealed class SubscribePushCommandHandler(INotificationsStore store, TimeProvider clock)
    : ICommandHandler<SubscribePushCommand>
{
    public async Task<Result> Handle(SubscribePushCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var valid = PushSubscriptionValidator.Validate(command.Endpoint, command.P256dh, command.Auth);
        if (valid.IsFailure)
        {
            return valid;
        }

        await store.SubscribeAsync(
            command.UserId, command.Endpoint!, command.P256dh!, command.Auth!, clock.GetUtcNow(), cancellationToken);
        return Result.Success();
    }
}

/// <summary>SPEC-0030 — <c>DELETE /api/v1/me/push-subscriptions</c>: remove a inscrição do aparelho (idempotente).</summary>
internal sealed record UnsubscribePushCommand(Guid UserId, string? Endpoint) : ICommand;

/// <summary>SPEC-0030 — remove a inscrição do fã com aquele endpoint; sem ela (ou sem endpoint), nada muda.</summary>
internal sealed class UnsubscribePushCommandHandler(INotificationsStore store) : ICommandHandler<UnsubscribePushCommand>
{
    public async Task<Result> Handle(UnsubscribePushCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!string.IsNullOrEmpty(command.Endpoint))
        {
            await store.UnsubscribeAsync(command.UserId, command.Endpoint, cancellationToken);
        }

        return Result.Success();
    }
}
