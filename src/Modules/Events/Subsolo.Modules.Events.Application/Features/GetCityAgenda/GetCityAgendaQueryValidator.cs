using FluentValidation;

namespace Subsolo.Modules.Events.Application.Features.GetCityAgenda;

/// <summary>
/// SPEC-0008 — slug em <c>^[a-z0-9-]{2,60}$</c> (<see cref="AgendaErrors.InvalidSlug"/>) e, com as duas datas,
/// <c>from ≤ to</c> e no máximo <see cref="AgendaPeriod.MaxDays"/> dias (<see cref="AgendaErrors.InvalidRange"/>);
/// o código vai no <c>ErrorCode</c> de cada falha.
/// </summary>
internal sealed class GetCityAgendaQueryValidator : AbstractValidator<GetCityAgendaQuery>
{
    public GetCityAgendaQueryValidator() => throw new NotImplementedException();
}
