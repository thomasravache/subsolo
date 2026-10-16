using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Subsolo.Common.Infrastructure;
using Subsolo.IntegrationTests.Common;
using Subsolo.Modules.Template.Infrastructure;
using Subsolo.Modules.TemplateB.Infrastructure;

namespace Subsolo.Common.IntegrationTests.Infrastructure;

/// <summary>
/// Host genérico com a infraestrutura comum e os módulos de exemplo Template e TemplateB, composto como o
/// Subsolo.Api compõe os módulos (<c>services.Add&lt;M&gt;Module(configuration)</c>), num database isolado e com o
/// relógio da aplicação (<see cref="TimeProvider"/>, usado também pelo Quartz) trocado por um
/// <see cref="FakeTimeProvider"/>.
/// </summary>
internal sealed class ModulesHost : IAsyncDisposable
{
    /// <summary>Quanto tempo real esperar, por padrão, a cada passo do relógio simulado, até a condição se cumprir.</summary>
    private static readonly TimeSpan DefaultRealWaitPerStep = TimeSpan.FromSeconds(2);

    private readonly IHost _host;
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

    public static async Task<ModulesHost> CreateAsync(PostgresFixture postgres, CancellationToken cancellationToken)
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

    /// <summary>Inicia o host (hosted services, entre eles o scheduler do Quartz).</summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _host.StartAsync(cancellationToken);
        _started = true;
    }

    /// <summary>
    /// Avança o relógio simulado em passos de <paramref name="step"/> até <paramref name="condition"/> valer ou o
    /// tempo simulado passar de <paramref name="limit"/>. Depois de cada passo, espera (tempo real) os jobs
    /// disparados rodarem (<paramref name="realWaitPerStep"/>, padrão 2 s). Devolve o tempo simulado decorrido
    /// quando a condição passou a valer, ou <see langword="null"/>.
    /// </summary>
    public async Task<TimeSpan?> AdvanceUntilAsync(
        Func<bool> condition,
        TimeSpan limit,
        TimeSpan step,
        CancellationToken cancellationToken,
        TimeSpan? realWaitPerStep = null)
    {
        var realWait = realWaitPerStep ?? DefaultRealWaitPerStep;
        var start = Clock.GetUtcNow();
        while (true)
        {
            if (await WaitRealTimeAsync(condition, realWait, cancellationToken))
            {
                return Clock.GetUtcNow() - start;
            }

            if (Clock.GetUtcNow() - start + step > limit)
            {
                return null;
            }

            Clock.Advance(step);
        }
    }

    /// <summary>Avança o relógio simulado por <paramref name="duration"/>, dando tempo real aos jobs a cada passo.</summary>
    public async Task AdvanceAsync(TimeSpan duration, TimeSpan step, CancellationToken cancellationToken)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += step)
        {
            Clock.Advance(step);
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_started)
        {
            await _host.StopAsync(CancellationToken.None);
        }

        _host.Dispose();
    }

    private static async Task<bool> WaitRealTimeAsync(Func<bool> condition, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        return condition();
    }
}
