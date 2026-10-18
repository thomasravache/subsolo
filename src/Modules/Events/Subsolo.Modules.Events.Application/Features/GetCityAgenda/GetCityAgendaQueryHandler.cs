using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Domain.Cities;

namespace Subsolo.Modules.Events.Application.Features.GetCityAgenda;

/// <summary>
/// SPEC-0008 — devolve a cidade e os shows do período; cidade inexistente → <see cref="AgendaErrors.CityNotFound"/>
/// (NotFound); período resolvido inválido (ex.: só <c>to</c>, antes de hoje) → <see cref="AgendaErrors.InvalidRange"/>
/// (Validation). SPEC-0023 (Emenda v2, b): sem <c>from</c> e <c>to</c>, os shows de 6 h atrás até 90 dias à frente;
/// com algum deles, os dias locais do período resolvido (SPEC-0008). Por data, com a situação.
/// </summary>
internal sealed class GetCityAgendaQueryHandler(ICityRepository cities, TimeProvider clock, IShowsReadModel shows)
    : IQueryHandler<GetCityAgendaQuery, CityAgendaResponse>
{
    /// <summary>Quanto antes de agora a janela padrão começa.</summary>
    public static readonly TimeSpan DefaultLookBack = TimeSpan.FromHours(6);

    /// <summary>Quanto à frente a janela padrão vai.</summary>
    public static readonly TimeSpan DefaultLookAhead = TimeSpan.FromDays(90);

    /// <summary>SPEC-0008 — sem leitura de shows (agenda sempre vazia), para os testes do contrato da agenda.</summary>
    public GetCityAgendaQueryHandler(ICityRepository cities, TimeProvider clock)
        : this(cities, clock, NoShows.Instance)
    {
    }

    public async Task<Result<CityAgendaResponse>> Handle(GetCityAgendaQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var city = await cities.GetBySlugAsync(query.Slug, cancellationToken);
        if (city is null)
        {
            return Result.Failure<CityAgendaResponse>(
                Error.NotFound(AgendaErrors.CityNotFound, $"Nenhuma cidade com o slug \"{query.Slug}\"."));
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(city.TimeZone);
        DateTimeOffset fromUtc;
        DateTimeOffset toUtc;
        if (query.From is null && query.To is null)
        {
            var now = clock.GetUtcNow();
            fromUtc = now - DefaultLookBack;
            toUtc = now + DefaultLookAhead;
        }
        else
        {
            var period = AgendaPeriod.Resolve(query.From, query.To, timeZone, clock);
            if (!period.IsValid)
            {
                return Result.Failure<CityAgendaResponse>(AgendaErrors.InvalidRangeError(period));
            }

            fromUtc = StartOfDay(period.From, timeZone);
            toUtc = StartOfDay(period.To.AddDays(1), timeZone);
        }

        var listed = await shows.ListAgendaAsync(city.Slug, fromUtc, toUtc, cancellationToken);
        return Result.Success(new CityAgendaResponse(
            new CityResponse(city.Slug, city.Name, city.State, city.TimeZone),
            listed));
    }

    /// <summary>O instante UTC da meia-noite local de <paramref name="date"/> (ou do primeiro horário que existir nela).</summary>
    private static DateTimeOffset StartOfDay(DateOnly date, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(30);
        }

        return new DateTimeOffset(local, timeZone.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>Leitura vazia (a agenda da SPEC-0008, antes do cadastro de shows).</summary>
    private sealed class NoShows : IShowsReadModel
    {
        public static readonly NoShows Instance = new();

        public Task<IReadOnlyList<ShowSummaryResponse>> ListAgendaAsync(
            string city, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ShowSummaryResponse>>([]);

        public Task<IReadOnlyList<ListMyShows.MyShowResponse>> ListMineAsync(
            IReadOnlyCollection<Guid> organizerProfileIds,
            Guid? createdBy,
            DateTimeOffset boundaryUtc,
            bool upcoming,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ListMyShows.MyShowResponse>>([]);

        public Task<GetShow.ShowDetails?> GetAsync(Guid showId, CancellationToken cancellationToken) =>
            Task.FromResult<GetShow.ShowDetails?>(null);
    }
}
