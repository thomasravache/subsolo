using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Subsolo.Observability.UnitTests;

/// <summary>Testes que trocam o <see cref="Console.Out"/> do processo: rodam sozinhos, sem paralelismo.</summary>
[CollectionDefinition(nameof(ProcessConsoleTests), DisableParallelization = true)]
public sealed class ProcessConsoleTests;

/// <summary>
/// SPEC-0009 UT-01 (Emenda 2, ADR-0008) — os logs de console de um host com <c>AddServiceDefaults()</c> também passam
/// pelo redator: o texto escrito no console não traz o e-mail cru (sai <c>j***@e***.com</c>) nem o token ou o cookie.
/// </summary>
[Collection(nameof(ProcessConsoleTests))]
public sealed class ConsoleRedactionTests
{
    private const string Email = "joao@exemplo.com";
    private const string MaskedEmail = "j***@e***.com";
    private const string Authorization = "Bearer abc";
    private const string Cookie = "sessao=c0ok1e-segredo-123";

    [Fact]
    [Trait("Spec", "SPEC-0009:UT-01")]
    public async Task ConsoleLogsDoNotContainRawEmailTokenOrCookie()
    {
        var marker = Guid.NewGuid().ToString("N");
        var console = await CaptureConsoleAsync(logger =>
        {
#pragma warning disable CA1848, CA1873, CA2254 // mensagem de log montada no teste, de propósito com dados sensíveis
            logger.LogInformation(
                "Login {Marker} de {Email} com Authorization {Authorization} e cookie {Cookie}",
                marker, Email, Authorization, Cookie);
#pragma warning restore CA1848, CA1873, CA2254
        });

        console.ShouldContain(marker, Case.Sensitive, "o log não foi escrito no console (pré-condição do teste)");
        console.ShouldNotContain(Email, Case.Insensitive, "o e-mail aparece cru no log de console");
        console.ShouldNotContain(Authorization, Case.Insensitive, "o token aparece cru no log de console");
        console.ShouldNotContain(Cookie, Case.Insensitive, "o cookie aparece cru no log de console");
        console.ShouldContain(MaskedEmail, Case.Sensitive, "o log de console não traz o e-mail mascarado");
    }

    /// <summary>
    /// Sobe um host com <c>AddServiceDefaults()</c> (com os provedores de log padrão do host, entre eles o console),
    /// executa <paramref name="log"/> e devolve o que foi escrito no console até o host ser descartado (o descarte
    /// esvazia a fila do logger de console).
    /// </summary>
    private static async Task<string> CaptureConsoleAsync(Action<ILogger> log)
    {
        var original = Console.Out;
        await using var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ApplicationName = "Subsolo.Observability.UnitTests",
                EnvironmentName = "Testing",
            });
            builder.AddServiceDefaults();
            using (var host = builder.Build())
            {
                log(host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Subsolo.Observability.UnitTests"));
            }

            await captured.FlushAsync(TestContext.Current.CancellationToken);
            return captured.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
