using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Quartz;
using Subsolo.Common.Infrastructure;
using Subsolo.Engagement.IntegrationTests.Infrastructure;
using Subsolo.Modules.Engagement.Infrastructure;

namespace Subsolo.Engagement.IntegrationTests;

/// <summary>
/// SPEC-0038 UT-03 (H36-03, H36-04) — com o relógio da aplicação num sábado (24/10/2026, 10h de São Paulo), a validação
/// de <c>Engagement:DigestCron</c> na subida olha os próximos 7 dias, não só o dia corrente: "0 0 9 ? * MON-FRI" (9h nos
/// dias úteis) sobe; "0 0 10 * * ?" (10h) continua impedindo a subida com mensagem que cita as 9h; e "isto não é cron"
/// impede a subida com mensagem própria de expressão inválida, sem citar as 9h. Mesmo host mínimo do
/// <see cref="DigestCronValidationTests"/> (SPEC-0036), com o <see cref="TimeProvider"/> trocado pelo
/// <see cref="RunningClock"/> no sábado (registrado antes da infraestrutura comum, que só registra o do sistema se não
/// houver outro).
/// </summary>
public sealed partial class DigestCronWeekValidationTests
{
    /// <summary>Sábado, 24/10/2026, 10h00 de São Paulo (13h00 UTC).</summary>
    private static readonly DateTimeOffset Saturday = new(2026, 10, 24, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Spec", "SPEC-0038:UT-03")]
    public async Task AWeekdaysOnlyDigestCronLetsTheHostStartOnASaturday()
    {
        Saturday.DayOfWeek.ShouldBe(DayOfWeek.Saturday);

        var failure = await StartupFailureAsync("0 0 9 ? * MON-FRI");

        failure.ShouldBeNull(
            $"Engagement:DigestCron = \"0 0 9 ? * MON-FRI\" dispara às 9h de São Paulo nos dias úteis e deveria subir no sábado: {failure}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0038:UT-03")]
    public async Task ADigestCronThatNeverFiresAtNineStopsTheHostOnASaturdayCitingNine()
    {
        var failure = await StartupFailureAsync("0 0 10 * * ?");

        failure.ShouldNotBeNull("Engagement:DigestCron = \"0 0 10 * * ?\" (10h) deveria impedir a subida da API");
        failure.ShouldNotBeOfType<NotImplementedException>();
        var message = Messages(failure);
        message.ShouldContain("DigestCron", Case.Sensitive, $"a mensagem não cita Engagement:DigestCron: {message}");
        NineOClock().IsMatch(message).ShouldBeTrue($"a mensagem não cita as 9h: {message}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0038:UT-03")]
    public async Task AnInvalidDigestCronStopsTheHostWithAnInvalidExpressionMessage()
    {
        var failure = await StartupFailureAsync("isto não é cron");

        failure.ShouldNotBeNull("Engagement:DigestCron = \"isto não é cron\" deveria impedir a subida da API");
        failure.ShouldNotBeOfType<NotImplementedException>();
        var message = Messages(failure);
        message.ShouldContain("DigestCron", Case.Sensitive, $"a mensagem não cita Engagement:DigestCron: {message}");
        message.ShouldContain("isto não é cron", Case.Sensitive, $"a mensagem não cita a expressão: {message}");
        InvalidExpression().IsMatch(message).ShouldBeTrue($"a mensagem não diz que a expressão cron é inválida: {message}");
        NineOClock().IsMatch(message).ShouldBeFalse($"a mensagem de expressão inválida não deveria citar as 9h: {message}");
    }

    /// <summary>
    /// Monta o host com <c>Engagement:DigestCron</c> = <paramref name="cron"/> e o relógio em <see cref="Saturday"/> e faz
    /// o que a subida faz antes de rodar os jobs (como em <see cref="DigestCronValidationTests"/>). Devolve a exceção (ou
    /// nula).
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
            builder.Services.AddSingleton<TimeProvider>(new RunningClock(Saturday));
            builder.Services.AddCommonInfrastructure(builder.Configuration);
            builder.Services.AddEngagementModule(builder.Configuration);
            using var host = builder.Build();

            host.Services.GetRequiredService<TimeProvider>().ShouldBeOfType<RunningClock>("o relógio do sábado não foi usado");
            host.Services.GetService<IStartupValidator>()?.Validate();
            _ = host.Services.GetRequiredService<IOptions<QuartzOptions>>().Value;
            var scheduler = await host.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(ct);
            await scheduler.Shutdown(waitForJobsToComplete: false, ct);
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not Shouldly.ShouldAssertException)
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

    /// <summary>"não é uma expressão cron válida" (a mensagem da SPEC-0038 §3).</summary>
    [GeneratedRegex(@"não é uma expressão cron válida")]
    private static partial Regex InvalidExpression();
}
