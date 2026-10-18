using Subsolo.Modules.Events.Domain.Shows;

namespace Subsolo.Modules.Events.Application.Features.GetCityAgenda;

/// <summary>SPEC-0023 — a situação gravada (<see cref="ShowStatuses"/>) no enum do contrato (<see cref="ShowStatus"/>).</summary>
internal static class ShowStatusMapping
{
    public static ShowStatus ToResponse(string status) => status switch
    {
        ShowStatuses.Cancelled => ShowStatus.Cancelled,
        ShowStatuses.Postponed => ShowStatus.Postponed,
        _ => ShowStatus.Scheduled,
    };
}
