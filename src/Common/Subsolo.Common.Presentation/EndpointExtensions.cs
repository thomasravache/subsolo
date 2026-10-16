using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Subsolo.Common.Presentation;

/// <summary>SPEC-0007 — descoberta de endpoints por módulo.</summary>
public static class EndpointExtensions
{
    /// <summary>Registra no contêiner todo <see cref="IEndpoint"/> concreto de <paramref name="assembly"/>.</summary>
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly) =>
        throw new NotImplementedException();

    /// <summary>
    /// Mapeia todos os <see cref="IEndpoint"/> registrados no contêiner, opcionalmente dentro de
    /// <paramref name="routeGroupBuilder"/>.
    /// </summary>
    public static IApplicationBuilder MapEndpoints(this WebApplication app, RouteGroupBuilder? routeGroupBuilder = null) =>
        throw new NotImplementedException();
}
