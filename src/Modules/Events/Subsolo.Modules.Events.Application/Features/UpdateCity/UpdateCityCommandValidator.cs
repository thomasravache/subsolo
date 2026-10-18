using FluentValidation;
using Subsolo.Modules.Events.Application.Features.CreateCity;

namespace Subsolo.Modules.Events.Application.Features.UpdateCity;

/// <summary>SPEC-0017 — as mesmas regras de nome, UF e fuso da criação → <c>events.invalid_city</c>.</summary>
internal sealed class UpdateCityCommandValidator : AbstractValidator<UpdateCityCommand>
{
    public UpdateCityCommandValidator()
    {
        RuleFor(command => command.Name).CityName();
        RuleFor(command => command.State).State();
        RuleFor(command => command.TimeZone).TimeZone();
    }
}
