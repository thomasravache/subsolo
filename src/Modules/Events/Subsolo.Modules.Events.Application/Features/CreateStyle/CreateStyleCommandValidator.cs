using FluentValidation;
using Subsolo.Modules.Events.Application.Features.ListStyles;

namespace Subsolo.Modules.Events.Application.Features.CreateStyle;

/// <summary>SPEC-0017 — nome com 2 a 30 caracteres → senão <c>events.invalid_style</c> (no <c>ErrorCode</c> da falha).</summary>
internal sealed class CreateStyleCommandValidator : AbstractValidator<CreateStyleCommand>
{
    public CreateStyleCommandValidator() => RuleFor(command => command.Name).StyleName();
}
