using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Quartz;
using Subsolo.Common.Infrastructure;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Template.Infrastructure;
using Subsolo.Modules.TemplateB.Infrastructure;

namespace Subsolo.Common.IntegrationTests.Infrastructure;

/// <summary>
/// Host genérico com a infraestrutura comum e os módulos de exemplo Template e TemplateB, composto como o
/// Subsolo.Api compõe os módulos (<c>services.Add&lt;M&gt;Module(configuration)</c>), num database isolado e com o
/// relógio da aplicação (<see cref="TimeProvider"/>, usado também pelo Quartz) trocado por um
/// <see cref="FakeTimeProvider"/>. Cada avanço do relógio espera as execuções dos jobs que ficaram devidas
/// (contadas por um <see cref="JobExecutionCounter"/>), em vez de uma espera fixa em tempo real.
/// </summary>
internal sealed class ModulesHost : IAsyncDisposable
{
    /// <summary>
    /// Proteção: tempo real máximo para os jobs devidos concluírem depois de um avanço do relógio. Estourar é falha
    /// do teste (o job não rodou), nunca um caminho normal.
    /// </summary>
    private static readonly TimeSpan JobsTimeout = TimeSpan.FromSeconds(30);

    private readonly IHost _host;
    private readonly JobExecutionCounter _jobs = new();
    private IReadOnlyList<JobKey> _jobKeys = [];
    private bool _started;

    private ModulesHost(IHost host, FakeTimeProvider clock, string connectionString)
    {
        _host = host;
        Clock = clock;
        ConnectionString = connectionString;
    }

    public FakeTimeProvider Clock { get; }

    public string ConnectionString { get; }

    public IServiceProvider Services => _host.Services;

    public static Task<ModulesHost> CreateAsync(PostgresFixture postgres, CancellationToken cancellationToken) =>
        CreateAsync(postgres, configureServices: null, cancellationToken);

    /// <summary>Como <see cref="CreateAsync(PostgresFixture, CancellationToken)"/>, com registros extras do teste
    /// (ex.: interceptors do EF Core) aplicados depois dos módulos.</summary>
    public static async Task<ModulesHost> CreateAsync(
        PostgresFixture postgres,
        Action<IServiceCollection>? configureServices,
        CancellationToken cancellationToken)
    {
        var connectionString = await IsolatedDatabase.CreateAsync(postgres, cancellationToken);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Testing" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{CommonInfrastructureExtensions.ConnectionStringName}"] = connectionString,
        });
        builder.Services.AddCommonInfrastructure(builder.Configuration);
        builder.Services.AddTemplateModule(builder.Configuration);
        builder.Services.AddTemplateBModule(builder.Configuration);
        builder.Services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock));
        configureServices?.Invoke(builder.Services);

        return new ModulesHost(builder.Build(), clock, connectionString);
    }

    /// <summary>Aplica as migrações do DbContext do módulo.</summary>
    public async Task MigrateAsync<TContext>(CancellationToken cancellationToken)
        where TContext : DbContext
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TContext>().Database.MigrateAsync(cancellationToken);
    }

    public async Task MigrateAllAsync(CancellationToken cancellationToken)
    {
        await MigrateAsync<TemplateDbContext>(cancellationToken);
        await MigrateAsync<TemplateBDbContext>(cancellationToken);
    }

    /// <summary>
    /// Inicia o host (hosted services, entre eles o scheduler do Quartz), com o <see cref="JobExecutionCounter"/>
    /// escutando todos os jobs, e espera a primeira execução de cada job (disparada no início).
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var scheduler = await Services.GetRequiredService<ISchedulerFactory>().GetScheduler(cancellationToken);
        scheduler.ListenerManager.AddJobListener(_jobs, GroupMatcher<JobKey>.AnyGroup());
        _jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken);
        _jobKeys.ShouldNotBeEmpty("nenhum job do Quartz agendado pelos módulos");

        await _host.StartAsync(cancellationToken);
        _started = true;
        await WaitForDueJobsAsync(cancellationToken);
    }

    /// <summary>
    /// Avança o relógio simulado em passos de <paramref name="step"/> até <paramref name="condition"/> valer ou o
    /// tempo simulado passar de <paramref name="limit"/>; depois de cada passo, espera os jobs devidos concluírem.
    /// Devolve o tempo simulado decorrido quando a condição passou a valer, ou <see langword="null"/>.
    /// </summary>
    public async Task<TimeSpan?> AdvanceUntilAsync(
        Func<bool> condition,
        TimeSpan limit,
        TimeSpan step,
        CancellationToken cancellationToken)
    {
        var start = Clock.GetUtcNow();
        while (true)
        {
            if (condition())
            {
                return Clock.GetUtcNow() - start;
            }

            if (Clock.GetUtcNow() - start + step > limit)
            {
                return null;
            }

            await StepAsync(step, cancellationToken);
        }
    }

    /// <summary>
    /// Avança o relógio simulado por <paramref name="duration"/> em passos de <paramref name="step"/>, esperando,
    /// a cada passo, que todo job tenha concluído a execução devida mais recente. Garante que as asserções feitas
    /// depois (ex.: "nunca mais depois do sucesso") observaram execuções reais dos jobs.
    /// </summary>
    public async Task AdvanceAsync(TimeSpan duration, TimeSpan step, CancellationToken cancellationToken)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += step)
        {
            await StepAsync(step, cancellationToken);
        }
    }

    /// <summary>Execuções concluídas, somadas, de todos os jobs com <paramref name="jobNameSuffix"/> no nome.</summary>
    public int CompletedExecutions(string jobNameSuffix) =>
        _jobKeys.Where(key => key.Name.EndsWith(jobNameSuffix, StringComparison.Ordinal)).Sum(_jobs.Completed);

    public async ValueTask DisposeAsync()
    {
        if (_started)
        {
            await _host.StopAsync(CancellationToken.None);
        }

        _host.Dispose();
    }

    private async Task StepAsync(TimeSpan step, CancellationToken cancellationToken)
    {
        Clock.Advance(step);
        await WaitForDueJobsAsync(cancellationToken);
    }

    /// <summary>
    /// Espera (tempo real, com <see cref="JobsTimeout"/> só como proteção) até que, para cada job, a última
    /// execução concluída seja a devida mais recente: os jobs disparam a cada
    /// <see cref="ModuleRegistrationExtensions.ProcessingInterval"/>, então há sempre um disparo agendado em
    /// (agora − intervalo, agora].
    /// </summary>
    private async Task WaitForDueJobsAsync(CancellationToken cancellationToken)
    {
        var dueAfter = Clock.GetUtcNow() - ModuleRegistrationExtensions.ProcessingInterval;
        var watch = Stopwatch.StartNew();
        while (true)
        {
            var pending = _jobKeys.Where(key => !(_jobs.LastCompletedScheduledFire(key) > dueAfter)).ToList();
            if (pending.Count == 0)
            {
                return;
            }

            if (watch.Elapsed > JobsTimeout)
            {
                throw new TimeoutException(
                    $"Jobs sem a execução devida até {Clock.GetUtcNow():O} em {JobsTimeout.TotalSeconds} s reais: " +
                    string.Join(", ", pending.Select(key => $"{key} (última: {_jobs.LastCompletedScheduledFire(key)?.ToString("O") ?? "nunca"})")));
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }
    }
}
