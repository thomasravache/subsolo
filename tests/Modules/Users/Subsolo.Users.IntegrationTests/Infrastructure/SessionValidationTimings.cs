using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Users.IntegrationTests.Infrastructure;

/// <summary>Duração de cada <see cref="ISessionValidator.ValidateAsync"/> feita pelo host (IT-04: p95 ≤ 5 ms).</summary>
internal sealed class SessionValidationTimings
{
    private readonly List<TimeSpan> _durations = [];

    public IReadOnlyList<TimeSpan> Durations
    {
        get
        {
            lock (_durations)
            {
                return [.. _durations];
            }
        }
    }

    public void Reset()
    {
        lock (_durations)
        {
            _durations.Clear();
        }
    }

    /// <summary>Percentil 95 das durações medidas.</summary>
    public TimeSpan P95()
    {
        var sorted = Durations.Order().ToList();
        return sorted.Count == 0 ? TimeSpan.Zero : sorted[(int)Math.Ceiling(sorted.Count * 0.95) - 1];
    }

    public void Record(TimeSpan duration)
    {
        lock (_durations)
        {
            _durations.Add(duration);
        }
    }

    /// <summary>Envolve o <see cref="ISessionValidator"/> registrado pelo módulo com a medição (se houver um registrado).</summary>
    public void Decorate(IServiceCollection services)
    {
        var original = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(ISessionValidator));
        if (original is null)
        {
            return;
        }

        services.Remove(original);
        services.Add(new ServiceDescriptor(
            typeof(ISessionValidator),
            provider => new TimedSessionValidator(Create(original, provider), this),
            original.Lifetime));
    }

    private static ISessionValidator Create(ServiceDescriptor descriptor, IServiceProvider provider) =>
        (ISessionValidator)(descriptor.ImplementationInstance
            ?? descriptor.ImplementationFactory?.Invoke(provider)
            ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!));

    private sealed class TimedSessionValidator(ISessionValidator inner, SessionValidationTimings timings) : ISessionValidator
    {
        public async Task<SessionPrincipal?> ValidateAsync(string sessionToken, CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                return await inner.ValidateAsync(sessionToken, cancellationToken);
            }
            finally
            {
                timings.Record(watch.Elapsed);
            }
        }
    }
}
