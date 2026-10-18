using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Subsolo.Modules.Users.Application;
using Subsolo.Modules.Users.Infrastructure;

namespace Subsolo.Users.IntegrationTests;

/// <summary>
/// SPEC-0016 UT-07 (Emenda 4, §7.6 j) — <c>App:PublicUrl</c> é validado na inicialização do host (o mesmo
/// <see cref="IStartupValidator"/> que o <c>Host.StartAsync</c> roda antes dos serviços): vazio, relativo
/// (<c>/entrar</c>) e <c>http://subsolo.test</c> em Production impedem a inicialização com mensagem que cita
/// <c>App:PublicUrl</c>; <c>https://subsolo.test</c> sobe; <c>http://localhost</c> em Development sobe. Sem banco nem
/// rede: só o registro do módulo Users num host mínimo.
/// </summary>
public sealed class PublicUrlValidationTests
{
    [Theory]
    [Trait("Spec", "SPEC-0016:UT-07")]
    [InlineData("", "Production")]
    [InlineData("/entrar", "Production")]
    [InlineData("http://subsolo.test", "Production")]
    [InlineData("", "Development")]
    [InlineData("/entrar", "Development")]
    public void InvalidPublicUrlStopsTheHostWithAMessageCitingTheKey(string publicUrl, string environment)
    {
        using var host = Host(publicUrl, environment);

        var validator = host.Services.GetService<IStartupValidator>();
        validator.ShouldNotBeNull("nenhuma opção validada na inicialização (ValidateOnStart) no módulo Users");
        var failure = Should.Throw<OptionsValidationException>(
            validator.Validate,
            $"App:PublicUrl = \"{publicUrl}\" em {environment} deveria impedir a inicialização");
        failure.Message.ShouldContain("App:PublicUrl", Case.Sensitive, "a mensagem não cita a chave App:PublicUrl");
    }

    /// <summary>
    /// SPEC-0019 §7.6 (herança da revisão da SPEC-0016) — a base do link mágico com query ou fragmento
    /// (<c>{App:PublicUrl}/entrar/confirmar?token=…</c> sairia quebrado) também impede a inicialização.
    /// </summary>
    [Theory]
    [Trait("Spec", "SPEC-0016:UT-07")]
    [Trait("Spec", "SPEC-0019:IT-05")]
    [InlineData("https://subsolo.test/?utm=1", "Production")]
    [InlineData("https://subsolo.test?utm=1", "Production")]
    [InlineData("https://subsolo.test/#topo", "Production")]
    [InlineData("https://subsolo.test#topo", "Production")]
    [InlineData("http://localhost:3000/?x=1", "Development")]
    [InlineData("http://localhost:3000/#x", "Development")]
    public void PublicUrlWithQueryOrFragmentStopsTheHost(string publicUrl, string environment)
    {
        using var host = Host(publicUrl, environment);

        var validator = host.Services.GetService<IStartupValidator>();
        validator.ShouldNotBeNull("nenhuma opção validada na inicialização (ValidateOnStart) no módulo Users");
        var failure = Should.Throw<OptionsValidationException>(
            validator.Validate,
            $"App:PublicUrl = \"{publicUrl}\" (com query ou fragmento) em {environment} deveria impedir a inicialização");
        failure.Message.ShouldContain("App:PublicUrl", Case.Sensitive, "a mensagem não cita a chave App:PublicUrl");
    }

    [Theory]
    [Trait("Spec", "SPEC-0016:UT-07")]
    [InlineData("https://subsolo.test", "Production")]
    [InlineData("https://subsolo.test", "Development")]
    [InlineData("http://localhost:3000", "Development")]
    public void ValidPublicUrlLetsTheHostStart(string publicUrl, string environment)
    {
        using var host = Host(publicUrl, environment);

        host.Services.GetService<IStartupValidator>()?.Validate();
        host.Services.GetRequiredService<IOptions<PublicUrlOptions>>().Value.PublicUrl.ShouldBe(publicUrl);
    }

    private static IHost Host(string publicUrl, string environment)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { EnvironmentName = environment, DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["App:PublicUrl"] = publicUrl,
            ["ConnectionStrings:subsolo"] = "Host=127.0.0.1;Database=nao-usado",
        });
        builder.Services.AddUsersModule(builder.Configuration);
        return builder.Build();
    }
}
