using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Quartz;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Users.Application;
using Subsolo.Modules.Users.Application.Diagnostics;
using Subsolo.Modules.Users.Application.Features.SignIn;
using Subsolo.Modules.Users.Application.Features.SignInWithGoogle;
using Subsolo.Modules.Users.Application.Features.ValidateSession;
using Subsolo.Modules.Users.Domain.MagicLinks;
using Subsolo.Modules.Users.Domain.Sessions;
using Subsolo.Modules.Users.Domain.Users;
using Subsolo.Modules.Users.Infrastructure.MagicLinks;
using Subsolo.Modules.Users.Infrastructure.Sessions;
using Subsolo.Modules.Users.Infrastructure.Users;
using Subsolo.Modules.Users.IntegrationEvents;
using Subsolo.Modules.Users.Presentation;
using Subsolo.Modules.Users.Presentation.Features.SignIn;
using Subsolo.Modules.Users.Presentation.Features.SignInWithGoogle;

namespace Subsolo.Modules.Users.Infrastructure;

/// <summary>SPEC-0014 — registro do módulo Users: <c>services.AddUsersModule(configuration)</c> no host.</summary>
public static class UsersModule
{
    /// <summary>Nome do <c>Meter</c> do módulo (<c>subsolo.users.sign_ins</c>, <c>subsolo.users.sessions_revoked</c>).</summary>
    public const string MeterName = UsersTelemetry.MeterName;

    /// <summary>
    /// SPEC-0019 (§7.6, herança da revisão da SPEC-0016) — teto absoluto da sessão (90 dias desde a entrada), exposto
    /// para o host definir a validade do cookie sem importar <c>Users.Domain</c>.
    /// </summary>
    public static TimeSpan SessionAbsoluteLifetime => Session.AbsoluteLifetime;

    /// <summary>
    /// Chama <c>AddModule&lt;UsersDbContext&gt;</c> e <c>AddEndpoints</c> com os assemblies do módulo e registra
    /// <c>Users:AdminEmails</c>, os repositórios, a unidade de trabalho, <see cref="ISignInService"/>,
    /// <see cref="ISessionValidator"/> e o emissor do cookie de sessão. SPEC-0016 — <c>App:PublicUrl</c> (base do link
    /// mágico, validado na inicialização), o repositório dos links e a limpeza periódica deles (<see cref="CleanupMagicLinkTokensJob"/>).
    /// SPEC-0020 — o login com Google (esquema OIDC e cookie externo), registrado só com <c>Users:Google</c> configurado.
    /// </summary>
    public static IServiceCollection AddUsersModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<UsersOptions>().Bind(configuration.GetSection(UsersOptions.SectionName));
        services.AddOptions<PublicUrlOptions>()
            .Bind(configuration.GetSection(PublicUrlOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<PublicUrlOptions>, PublicUrlOptionsValidator>());
        services.AddHttpContextAccessor();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IMagicLinkTokenRepository, MagicLinkTokenRepository>();
        services.AddScoped<IUsersUnitOfWork>(provider => provider.GetRequiredService<UsersDbContext>());
        services.AddScoped<ISessionCookieIssuer, HttpSessionCookieIssuer>();
        services.AddScoped<ISignInService, SignInService>();
        services.AddScoped<ISessionValidator, SessionValidator>();
        services.AddScoped<GoogleSignInCallback>();
        // SPEC-0021 (Emenda v2): scaffold do contrato — os e-mails para a fila do admin do Profiles.
        services.AddScoped<IUsersDirectory>(_ =>
            throw new NotImplementedException("SPEC-0021: IUsersDirectory ainda não implementado."));

        var google = configuration.GetSection(UsersOptions.SectionName).Get<UsersOptions>()?.Google ?? new GoogleOptions();
        services.AddAuthentication().AddGoogleSignIn(google);

        services.AddModule<UsersDbContext>(
            configuration,
            typeof(UsersModule).Assembly,
            typeof(ISignInService).Assembly);
        services.ConfigureAllQuartzSchedulers(CleanupMagicLinkTokensJob.Schedule);
        return services.AddEndpoints(AssemblyReference.Assembly);
    }
}
