using FluentValidation;

namespace Subsolo.Modules.Moderation.Application.Features.ReportShow;

/// <summary>
/// SPEC-0033 — scaffold do validador da denúncia (UT-01): motivo conhecido, detalhe até 500 e obrigatório em
/// <c>other</c>; a falha leva <see cref="ModerationErrors.InvalidReport"/> no campo. Implementação pendente.
/// </summary>
internal sealed class ReportShowCommandValidator : AbstractValidator<ReportShowCommand>
{
    public ReportShowCommandValidator() => throw new NotImplementedException("SPEC-0033: validador da denúncia");
}
