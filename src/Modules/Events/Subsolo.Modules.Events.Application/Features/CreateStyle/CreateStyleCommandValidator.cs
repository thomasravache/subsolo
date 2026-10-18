using FluentValidation;

namespace Subsolo.Modules.Events.Application.Features.CreateStyle;

/// <summary>SPEC-0017 — nome com 2 a 30 caracteres → senão <c>events.invalid_style</c> (no <c>ErrorCode</c> da falha).</summary>
internal sealed class CreateStyleCommandValidator : AbstractValidator<CreateStyleCommand>
{
    // SPEC-0017: regras ainda não implementadas.
}
