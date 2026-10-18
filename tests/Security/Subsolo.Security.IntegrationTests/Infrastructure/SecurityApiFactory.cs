using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Presentation;

namespace Subsolo.Security.IntegrationTests.Infrastructure;

/// <summary>
/// O Subsolo.Api em memória (<see cref="WebApplicationFactory{TEntryPoint}"/>) no ambiente pedido, com o banco do
/// teste e, opcionalmente, os dublês de segurança: os <see cref="SecurityTestEndpoints"/>, o
/// <see cref="TestAuthHandler"/> como esquema de autenticação padrão (só a autenticação; desafio e proibição seguem
/// os da aplicação) e o IP de origem tirado do cabeçalho <see cref="RemoteIpHeader"/> (o TestServer não tem IP).
/// </summary>
internal sealed class SecurityApiFactory(string environment, string connectionString, bool withTestDoubles = true)
    : WebApplicationFactory<Program>
{
    public const string RemoteIpHeader = "X-Test-Remote-Ip";

    /// <summary>IP de origem quando a requisição não manda <see cref="RemoteIpHeader"/>.</summary>
    public static readonly IPAddress DefaultRemoteIp = IPAddress.Parse("203.0.113.10");

    /// <summary>Cliente HTTPS (cookies <c>Secure</c>/<c>__Host-</c> voltam ao servidor) que guarda os cookies.</summary>
    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:subsolo", connectionString);
        // SPEC-0016 (Emenda 5): a API valida App:PublicUrl na inicialização (https; http só em Development) e não tem
        // valor padrão no código; o host de teste roda em qualquer ambiente com o endereço do cliente de teste.
        builder.UseSetting("App:PublicUrl", "https://localhost");
        if (!withTestDoubles)
        {
            return;
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddEndpoints(typeof(SecurityApiFactory).Assembly);
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName);
            services.AddTransient<IStartupFilter, RemoteIpStartupFilter>();
        });
    }

    /// <summary>Define <c>Connection.RemoteIpAddress</c> antes do pipeline da aplicação.</summary>
    private sealed class RemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress =
                    context.Request.Headers.TryGetValue(RemoteIpHeader, out var ip) && IPAddress.TryParse(ip, out var parsed)
                        ? parsed
                        : DefaultRemoteIp;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
