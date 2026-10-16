using FluentValidation;

namespace Subsolo.Modules.Template.Application.Features.RenameTemplateItem;

/// <summary>Dublê de teste: exige o id e um nome não vazio.</summary>
internal sealed class RenameTemplateItemValidator : AbstractValidator<RenameTemplateItemCommand>
{
    public RenameTemplateItemValidator()
    {
        RuleFor(c => c.ItemId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty();
    }
}
