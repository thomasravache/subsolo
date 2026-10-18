using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application.Features.CreateCity;
using Subsolo.Modules.Events.Domain.Cities;

namespace Subsolo.Modules.Events.Application.Features.UpdateCity;

/// <summary>
/// SPEC-0017 — troca nome, UF e fuso da cidade (o slug não muda); cidade inexistente → <c>events.city_not_found</c>
/// (NotFound). Registra no log de auditoria quem editou.
/// </summary>
internal sealed partial class UpdateCityCommandHandler(
    ICityAdminRepository cities,
    IEventsUnitOfWork unitOfWork,
    ILogger<UpdateCityCommandHandler> logger)
    : ICommandHandler<UpdateCityCommand, AdminCityResponse>
{
    public async Task<Result<AdminCityResponse>> Handle(UpdateCityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var city = await cities.GetForUpdateAsync(command.Slug, cancellationToken);
        if (city is null)
        {
            return Result.Failure<AdminCityResponse>(EventsErrors.CityNotFoundError(command.Slug));
        }

        city.Update(command.Name.Trim(), command.State, command.TimeZone);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogCityUpdated(logger, command.AdminId, city.Slug);
        return Result.Success(new AdminCityResponse(city.Slug, city.Name, city.State, city.TimeZone));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Auditoria: admin {AdminId} editou a cidade {CitySlug}")]
    private static partial void LogCityUpdated(ILogger logger, Guid? adminId, string citySlug);
}
