using FluentValidation;

namespace Subsolo.Modules.Template.Application.Features.CreateTemplateNote;

/// <summary>Dublê de teste: exige um texto não vazio.</summary>
internal sealed class CreateTemplateNoteValidator : AbstractValidator<CreateTemplateNoteCommand>
{
    public CreateTemplateNoteValidator() => RuleFor(c => c.Text).NotEmpty();
}
