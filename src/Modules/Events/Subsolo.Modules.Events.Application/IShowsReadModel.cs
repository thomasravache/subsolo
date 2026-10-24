using Subsolo.Modules.Events.Application.Features.GetCityAgenda;
using Subsolo.Modules.Events.Application.Features.GetShow;
using Subsolo.Modules.Events.Application.Features.GetShowDetail;
using Subsolo.Modules.Events.Application.Features.ListMyShows;
using Subsolo.Modules.Events.Application.Features.ListProfileShows;

namespace Subsolo.Modules.Events.Application;

/// <summary>SPEC-0023 — leituras de <c>events.shows</c> para as consultas (sem rastreamento, sem passar pelo domínio).</summary>
internal interface IShowsReadModel
{
    /// <summary>
    /// Os shows da cidade <paramref name="city"/> com início em [<paramref name="fromUtc"/>, <paramref name="toUtc"/>), por
    /// data. SPEC-0025: com <paramref name="styles"/>, só os que têm algum desses estilos; os estilos que não existem são
    /// ignorados (nenhum existente = sem filtro de estilo).
    /// </summary>
    Task<IReadOnlyList<ShowSummaryResponse>> ListAgendaAsync(
        string city,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        IReadOnlyCollection<string> styles,
        CancellationToken cancellationToken);

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

    /// <summary>
    /// SPEC-0026 — até <paramref name="limit"/> shows em que <paramref name="profileId"/> é organizador, casa ou banda do
    /// line-up: os próximos (início a partir de <paramref name="boundaryUtc"/>, por data) ou os passados (antes dele, mais
    /// recentes primeiro).
    /// </summary>
    Task<IReadOnlyList<ProfileShowResponse>> ListByProfileAsync(
        Guid profileId,
        DateTimeOffset boundaryUtc,
        bool upcoming,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>O show para a tela de edição (inclusive o oculto pela moderação, com <c>Hidden</c>), ou <see langword="null"/>.</summary>
    Task<ShowDetails?> GetAsync(Guid showId, CancellationToken cancellationToken);

    /// <summary>
    /// SPEC-0024 — o show <paramref name="slug"/> da cidade <paramref name="city"/> com as medidas do flyer, ou
    /// <see langword="null"/> (SPEC-0033: também se oculto pela moderação).
    /// </summary>
    Task<PublicShowDetails?> GetBySlugAsync(string city, string slug, CancellationToken cancellationToken);
}
