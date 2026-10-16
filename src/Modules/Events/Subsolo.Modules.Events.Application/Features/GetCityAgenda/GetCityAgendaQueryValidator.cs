using FluentValidation;

namespace Subsolo.Modules.Events.Application.Features.GetCityAgenda;

/// <summary>
/// SPEC-0008 — slug em <c>^[a-z0-9-]{2,60}$</c> (<see cref="AgendaErrors.InvalidSlug"/>) e, com as duas datas,
/// <c>from ≤ to</c> e no máximo <see cref="AgendaPeriod.MaxDays"/> dias (<see cref="AgendaErrors.InvalidRange"/>);
/// o código vai no <c>ErrorCode</c> de cada falha.
/// </summary>
internal sealed class GetCityAgendaQueryValidator : AbstractValidator<GetCityAgendaQuery>
{
    public GetCityAgendaQueryValidator()
    {
        RuleFor(query => query.Slug)
            .Matches(GetCityAgendaQuery.SlugPattern)
            .WithErrorCode(AgendaErrors.InvalidSlug)
            .WithMessage("O slug da cidade deve ter de 2 a 60 letras minúsculas, dígitos ou hífens.");

        RuleFor(query => query.To)
            .Must((query, to) => AgendaPeriod.IsValidRange(query.From!.Value, to!.Value))
            .When(query => query.From.HasValue && query.To.HasValue)
            .WithErrorCode(AgendaErrors.InvalidRange)
            .WithMessage($"O período deve ter \"to\" igual ou depois de \"from\" e no máximo {AgendaPeriod.MaxDays} dias.");
    }
}
