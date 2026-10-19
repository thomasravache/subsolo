using System.Diagnostics;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Domain.Cities;

namespace Subsolo.Modules.Events.Application.Features.GetCityAgenda;

/// <summary>
/// SPEC-0008 — devolve a cidade e os shows do período; cidade inexistente → <see cref="AgendaErrors.CityNotFound"/>
/// (NotFound); período resolvido inválido (ex.: só <c>to</c>, antes de hoje) → <see cref="AgendaErrors.InvalidRange"/>
/// (Validation). SPEC-0023 (Emenda v2, b): sem <c>from</c> e <c>to</c>, os shows de 6 h atrás até 90 dias à frente;
/// com algum deles, os dias locais do período resolvido (SPEC-0008). Por data, com a situação. SPEC-0025: filtro por
/// estilos (os inexistentes são ignorados), o <c>period</c> em datas locais (sem <c>from</c>/<c>to</c>: hoje a hoje + 90)
/// e o <c>total</c>; o tipo de período e o número de estilos vão como tags no span da requisição.
/// </summary>
internal sealed class GetCityAgendaQueryHandler(ICityRepository cities, TimeProvider clock, IShowsReadModel shows)
    : IQueryHandler<GetCityAgendaQuery, CityAgendaResponse>
{
    /// <summary>Quanto antes de agora a janela padrão começa.</summary>
    public static readonly TimeSpan DefaultLookBack = TimeSpan.FromHours(6);

    /// <summary>Quanto à frente a janela padrão vai.</summary>
    public static readonly TimeSpan DefaultLookAhead = TimeSpan.FromDays(90);

    /// <summary>SPEC-0025 — tag do span: <c>default</c> (janela padrão) ou <c>range</c> (com <c>from</c>/<c>to</c>).</summary>
    public const string PeriodTag = "subsolo.agenda.period";

    /// <summary>SPEC-0025 — tag do span: quantos estilos vieram no filtro.</summary>
    public const string StylesCountTag = "subsolo.agenda.styles_count";

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
        var styles = query.Styles ?? [];
        var isDefault = query.From is null && query.To is null;
        Activity.Current?.SetTag(PeriodTag, isDefault ? "default" : "range");
        Activity.Current?.SetTag(StylesCountTag, styles.Count);
        DateTimeOffset fromUtc;
        DateTimeOffset toUtc;
        AgendaPeriodResponse periodResponse;
        if (isDefault)
        {
            var now = clock.GetUtcNow();
            fromUtc = now - DefaultLookBack;
            toUtc = now + DefaultLookAhead;
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
            periodResponse = new AgendaPeriodResponse(today, today.AddDays(DefaultLookAhead.Days));
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
            periodResponse = new AgendaPeriodResponse(period.From, period.To);
        }

        var listed = await shows.ListAgendaAsync(city.Slug, fromUtc, toUtc, styles, cancellationToken);
        return Result.Success(new CityAgendaResponse(
            new CityResponse(city.Slug, city.Name, city.State, city.TimeZone),
            listed)
        {
            Period = periodResponse,
            Total = listed.Count,
        });
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
}
