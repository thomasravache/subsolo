using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Domain.Cities;
using Subsolo.Modules.Events.Domain.Slugs;

namespace Subsolo.Modules.Events.Application.Features.CreateCity;

/// <summary>
/// SPEC-0017 — cria a cidade com o slug informado ou, sem ele, gerado do nome (gerado reservado →
/// <c>events.city_slug_reserved</c>; gerado fora do formato → <c>events.invalid_city</c>); slug já usado →
/// <c>events.city_slug_taken</c> (Conflict). Registra no log de auditoria quem criou.
/// </summary>
internal sealed partial class CreateCityCommandHandler(
    ICityAdminRepository cities,
    IEventsUnitOfWork unitOfWork,
    ILogger<CreateCityCommandHandler> logger)
    : ICommandHandler<CreateCityCommand, AdminCityResponse>
{
    public async Task<Result<AdminCityResponse>> Handle(CreateCityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var name = command.Name.Trim();
        var slug = command.Slug ?? SlugGenerator.FromName(name);
        if (City.IsReservedSlug(slug))
        {
            return Result.Failure<AdminCityResponse>(EventsErrors.FieldError(
                EventsErrors.CitySlugReserved, "Endereço reservado.", nameof(command.Slug), CityRules.ReservedSlugMessage));
        }

        if (!Regex.IsMatch(slug, SlugGenerator.Pattern, RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            return Result.Failure<AdminCityResponse>(EventsErrors.FieldError(
                EventsErrors.InvalidCity, "Dados da cidade inválidos.", nameof(command.Slug), EventsErrors.GeneratedSlugMessage));
        }

        if (await cities.ExistsAsync(slug, cancellationToken))
        {
            return Result.Failure<AdminCityResponse>(EventsErrors.CitySlugTakenError());
        }

        var city = City.Create(slug, name, command.State, command.TimeZone);
        cities.Add(city);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateSlugException)
        {
            // Outro pedido gravou o mesmo slug entre a conferência e a gravação (Emenda 2).
            return Result.Failure<AdminCityResponse>(EventsErrors.CitySlugTakenError());
        }

        LogCityCreated(logger, command.AdminId, city.Slug);
        return Result.Success(new AdminCityResponse(city.Slug, city.Name, city.State, city.TimeZone));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Auditoria: admin {AdminId} criou a cidade {CitySlug}")]
    private static partial void LogCityCreated(ILogger logger, Guid? adminId, string citySlug);
}
