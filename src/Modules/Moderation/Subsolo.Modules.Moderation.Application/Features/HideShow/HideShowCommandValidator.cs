using FluentValidation;

namespace Subsolo.Modules.Moderation.Application.Features.HideShow;

/// <summary>
/// SPEC-0033 — a nota interna é opcional e cabe na coluna <c>decision_note</c> (até 200 caracteres); a falha leva
/// <see cref="ModerationErrors.InvalidNote"/>, ligada ao campo <c>Note</c>.
/// </summary>
internal sealed class HideShowCommandValidator : AbstractValidator<HideShowCommand>
{
    public HideShowCommandValidator() =>
        RuleFor(command => command.Note)
            .Must(note => note is null || note.Trim().Length <= ModerationErrors.NoteMaxLength)
            .WithMessage($"A nota interna cabe em até {ModerationErrors.NoteMaxLength} caracteres.")
            .WithErrorCode(ModerationErrors.InvalidNote);
}
