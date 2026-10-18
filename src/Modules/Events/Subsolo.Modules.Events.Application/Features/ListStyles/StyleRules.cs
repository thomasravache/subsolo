using FluentValidation;

namespace Subsolo.Modules.Events.Application.Features.ListStyles;

/// <summary>SPEC-0017 — nome de estilo com 2 a 30 caracteres (criar e renomear) → senão <c>events.invalid_style</c>.</summary>
internal static class StyleRules
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 30;

    public static IRuleBuilderOptions<T, string> StyleName<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(name => name?.Trim().Length is >= NameMinLength and <= NameMaxLength)
            .WithErrorCode(EventsErrors.InvalidStyle)
            .WithMessage($"O nome do estilo deve ter de {NameMinLength} a {NameMaxLength} caracteres.");
}
