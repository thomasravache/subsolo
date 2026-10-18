using FluentValidation;
using Subsolo.Modules.Events.Application.Features.ListStyles;

namespace Subsolo.Modules.Events.Application.Features.RenameStyle;

/// <summary>SPEC-0017 — nome com 2 a 30 caracteres → senão <c>events.invalid_style</c> (no <c>ErrorCode</c> da falha).</summary>
internal sealed class RenameStyleCommandValidator : AbstractValidator<RenameStyleCommand>
{
    public RenameStyleCommandValidator() => RuleFor(command => command.Name).StyleName();
}
