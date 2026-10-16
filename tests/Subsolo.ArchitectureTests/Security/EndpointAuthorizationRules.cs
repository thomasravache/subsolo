using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Presentation;
using Xunit.Sdk;

namespace Subsolo.ArchitectureTests.Security;

/// <summary>
/// SPEC-0010 — regra do ADR-0003 (autorização negada por padrão): todo endpoint declara
/// <c>RequireAuthorization(&lt;política&gt;)</c> ou <c>AllowAnonymous()</c> explicitamente.
/// </summary>
public static class EndpointAuthorizationRules
{
    /// <summary>Texto da regra, citado na falha.</summary>
    public const string Rule =
        "ADR-0003: todo endpoint declara RequireAuthorization(<política>) ou AllowAnonymous() explicitamente";

    /// <summary>Os endpoints que <paramref name="endpoints"/> mapeiam, num host mínimo (sem servidor).</summary>
    public static IReadOnlyList<RouteEndpoint> EndpointsOf(params IEndpoint[] endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var builder = new MinimalEndpointRouteBuilder();
        foreach (var endpoint in endpoints)
        {
            endpoint.MapEndpoint(builder);
        }

        return [.. builder.DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()];
    }

    /// <summary>Os endpoints de todo assembly <c>Subsolo.Modules.*</c> ao lado do assembly de teste.</summary>
    public static IReadOnlyList<RouteEndpoint> ModuleEndpoints() =>
        EndpointsOf([.. ModularMonolithRules.ModuleAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(IEndpoint)))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => (IEndpoint)Activator.CreateInstance(type, nonPublic: true)!)]);

    /// <summary>
    /// Falha (exceção) nomeando cada endpoint sem <c>RequireAuthorization</c> nem <c>AllowAnonymous</c> explícito.
    /// </summary>
    public static void Check(IEnumerable<RouteEndpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var undeclared = endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
                && endpoint.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(Describe)
            .ToList();
        if (undeclared.Count > 0)
        {
            throw new XunitException($"{Rule}; sem declaração: {string.Join("; ", undeclared)}");
        }
    }

    private static string Describe(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
        return $"{string.Join(",", methods)} {endpoint.RoutePattern.RawText ?? endpoint.DisplayName}".Trim();
    }

    /// <summary>Construtor de rotas sem servidor: só o necessário para os <c>Map*</c> gerarem os endpoints.</summary>
    private sealed class MinimalEndpointRouteBuilder : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } =
            new HandlerServicesProvider(new ServiceCollection().AddLogging().AddRouting().BuildServiceProvider());

        public ICollection<EndpointDataSource> DataSources { get; } = [];

        public IApplicationBuilder CreateApplicationBuilder() =>
            throw new NotSupportedException("O host mínimo da regra de autorização não monta pipeline.");
    }

    /// <summary>
    /// O contêiner de cada módulo não é montado aqui: parâmetros de interface ou abstratos dos handlers (validadores,
    /// handlers de consulta) contam como serviços, como no host real, em vez de corpo da requisição.
    /// </summary>
    private sealed class HandlerServicesProvider(ServiceProvider inner) : IServiceProvider, IServiceProviderIsService
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IServiceProviderIsService) ? this : inner.GetService(serviceType);

        public bool IsService(Type serviceType) =>
            serviceType.IsInterface
            || serviceType.IsAbstract
            || inner.GetRequiredService<IServiceProviderIsService>().IsService(serviceType);
    }
}
