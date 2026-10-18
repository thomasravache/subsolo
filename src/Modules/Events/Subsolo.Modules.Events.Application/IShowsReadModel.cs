using Subsolo.Modules.Events.Application.Features.GetCityAgenda;
using Subsolo.Modules.Events.Application.Features.GetShow;
using Subsolo.Modules.Events.Application.Features.ListMyShows;

namespace Subsolo.Modules.Events.Application;

/// <summary>SPEC-0023 — leituras de <c>events.shows</c> para as consultas (sem rastreamento, sem passar pelo domínio).</summary>
internal interface IShowsReadModel
{
    /// <summary>Os shows da cidade <paramref name="city"/> com início em [<paramref name="fromUtc"/>, <paramref name="toUtc"/>), por data.</summary>
    Task<IReadOnlyList<ShowSummaryResponse>> ListAgendaAsync(
        string city, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Os shows organizados por um dos <paramref name="organizerProfileIds"/> ou, com <paramref name="createdBy"/>, os
    /// cadastrados por ele; os próximos (início a partir de <paramref name="boundaryUtc"/>, por data) ou os passados (antes
    /// dele, mais recentes primeiro).
    /// </summary>
    Task<IReadOnlyList<MyShowResponse>> ListMineAsync(
        IReadOnlyCollection<Guid> organizerProfileIds,
        Guid? createdBy,
        DateTimeOffset boundaryUtc,
        bool upcoming,
        CancellationToken cancellationToken);

    /// <summary>O show para a tela de edição, ou <see langword="null"/>.</summary>
    Task<ShowDetails?> GetAsync(Guid showId, CancellationToken cancellationToken);
}
