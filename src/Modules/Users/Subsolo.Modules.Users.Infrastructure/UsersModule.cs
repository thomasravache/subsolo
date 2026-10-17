using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Modules.Users.Application.Features.SignIn;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Modules.Users.Infrastructure;

/// <summary>SPEC-0014 — registro do módulo Users: <c>services.AddUsersModule(configuration)</c> no host.</summary>
public static class UsersModule
{
    /// <summary>
    /// Scaffold do contrato: os serviços públicos do módulo existem no contêiner, mas ainda não estão implementados
    /// (a implementação chama <c>AddModule&lt;UsersDbContext&gt;</c> e <c>AddEndpoints</c>, como o módulo Events).
    /// </summary>
    public static IServiceCollection AddUsersModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddScoped<ISignInService>(_ =>
            throw new NotImplementedException("SPEC-0014: ISignInService ainda não implementado."));
        services.AddScoped<ISessionValidator>(_ =>
            throw new NotImplementedException("SPEC-0014: ISessionValidator ainda não implementado."));
        return services;
    }
}
