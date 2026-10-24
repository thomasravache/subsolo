using FluentValidation;
using Subsolo.Modules.Moderation.Domain.Reports;

namespace Subsolo.Modules.Moderation.Application.Features.ReportShow;

/// <summary>
/// SPEC-0033 (UT-01) — o validador da denúncia: motivo entre os quatro do §6 (exato, sem diferenciar caixa nunca),
/// detalhe até 500 caracteres e obrigatório (não vazio) em <c>other</c>; a falha leva
/// <see cref="ModerationErrors.InvalidReport"/> no <c>ErrorCode</c>, ligada ao campo (<c>Reason</c> ou <c>Details</c>).
/// </summary>
internal sealed class ReportShowCommandValidator : AbstractValidator<ReportShowCommand>
{
    public ReportShowCommandValidator()
    {
        RuleFor(command => command.Reason)
            .Must(reason => reason is not null && ReportReasons.All.Contains(reason, StringComparer.Ordinal))
            .WithMessage("Escolha o motivo: fake, cancelled_unannounced, wrong_info ou other.")
            .WithErrorCode(ModerationErrors.InvalidReport);

        RuleFor(command => command.Details)
            .Must(details => details is null || details.Length <= ReportReasons.DetailsMaxLength)
            .WithMessage($"Conte em até {ReportReasons.DetailsMaxLength} caracteres.")
            .WithErrorCode(ModerationErrors.InvalidReport);

        RuleFor(command => command.Details)
            .Must(details => !string.IsNullOrWhiteSpace(details))
            .When(command => command.Reason == ReportReasons.Other)
            .WithMessage("Conte o que está errado no show.")
            .WithErrorCode(ModerationErrors.InvalidReport);
    }
}
