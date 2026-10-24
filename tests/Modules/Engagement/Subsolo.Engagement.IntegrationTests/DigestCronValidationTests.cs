using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Quartz;
using Subsolo.Common.Infrastructure;
using Subsolo.Modules.Engagement.Infrastructure;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0036 UT-02 (H32-02) — a recuperação do resumo usa as 9h de São Paulo, então <c>Engagement:DigestCron</c> é
/// validado na subida do host (como <c>Engagement:ReminderHour</c>, SPEC-0032): uma expressão que não dispara às 09h00 de
/// São Paulo ("0 0 10 * * ?") impede a subida com mensagem que cita <c>DigestCron</c> e as 9h; "0 0 9 * * ?" (o padrão) e
/// "0 * * * * ?" (a cada minuto, da pilha local e do E2E) sobem. Sem banco nem rede: só o registro do módulo Engagement
/// (com a infraestrutura comum, que traz o Quartz) num host mínimo, montando as opções validadas e o scheduler do Quartz
/// (onde os gatilhos do módulo são agendados), como no <c>PublicUrlValidationTests</c> da SPEC-0016.
/// </summary>
public sealed partial class DigestCronValidationTests
{
    [Fact]
    [Trait("Spec", "SPEC-0036:UT-02")]
    public async Task ADigestCronThatDoesNotFireAtNineInSaoPauloStopsTheHost()
    {
        var failure = await StartupFailureAsync("0 0 10 * * ?");

        failure.ShouldNotBeNull("Engagement:DigestCron = \"0 0 10 * * ?\" (10h) deveria impedir a subida da API");
        failure.ShouldNotBeOfType<NotImplementedException>();
        var message = Messages(failure);
        message.ShouldContain("DigestCron", Case.Sensitive, $"a mensagem não cita Engagement:DigestCron: {message}");
        NineOClock().IsMatch(message).ShouldBeTrue($"a mensagem não cita as 9h: {message}");
    }

    [Theory]
    [Trait("Spec", "SPEC-0036:UT-02")]
    [InlineData("0 0 9 * * ?")]
    [InlineData("0 * * * * ?")]
    public async Task ADigestCronThatFiresAtNineInSaoPauloLetsTheHostStart(string cron)
    {
        var failure = await StartupFailureAsync(cron);

        failure.ShouldBeNull($"Engagement:DigestCron = \"{cron}\" dispara às 9h de São Paulo e deveria subir: {failure}");
    }

    /// <summary>
    /// Monta o host com <c>Engagement:DigestCron</c> = <paramref name="cron"/> e faz o que a subida faz antes de rodar os
    /// jobs: valida as opções (<see cref="IStartupValidator"/>), lê as opções do Quartz e cria o scheduler (com os
    /// gatilhos). Devolve a exceção (ou nula).
    /// </summary>
    private static async Task<Exception?> StartupFailureAsync(string cron)
    {
        var ct = TestContext.Current.CancellationToken;
        try
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                EnvironmentName = "Development",
                ApplicationName = "Subsolo.Api",
                DisableDefaults = true,
            });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [$"ConnectionStrings:{CommonInfrastructureExtensions.ConnectionStringName}"] =
                    "Host=localhost;Database=subsolo_validacao;Username=subsolo;Password=subsolo",
                ["Media:Root"] = Path.GetTempPath(),
                ["Engagement:DigestCron"] = cron,
            });
            builder.Services.AddCommonInfrastructure(builder.Configuration);
            builder.Services.AddEngagementModule(builder.Configuration);
            using var host = builder.Build();

            host.Services.GetService<IStartupValidator>()?.Validate();
            _ = host.Services.GetRequiredService<IOptions<QuartzOptions>>().Value;
            var scheduler = await host.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(ct);
            await scheduler.Shutdown(waitForJobsToComplete: false, ct);
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return exception;
        }
    }

    /// <summary>As mensagens da exceção e das internas (sem a pilha).</summary>
    private static string Messages(Exception exception)
    {
        var messages = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" | ", messages);
    }

    /// <summary>"9h", "9 h", "09h", "09h00" ou "09:00".</summary>
    [GeneratedRegex(@"(?<!\d)0?9(?:\s?h|:00)")]
    private static partial Regex NineOClock();
}
