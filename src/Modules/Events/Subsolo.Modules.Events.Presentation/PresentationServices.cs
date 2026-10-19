using Microsoft.Extensions.DependencyInjection;
using Subsolo.Modules.Events.Presentation.Features.GetCityAgenda;

namespace Subsolo.Modules.Events.Presentation;

/// <summary>SPEC-0025 — serviços da camada de apresentação do módulo (além dos endpoints).</summary>
public static class PresentationServices
{
    /// <summary>Registra o tratador que converte a falha de leitura das datas da agenda em 400 <c>agenda.invalid_range</c>.</summary>
    public static IServiceCollection AddEventsPresentation(this IServiceCollection services)
    {
        services.AddExceptionHandler<AgendaDateBindingExceptionHandler>();
        return services;
    }
}
